// <copyright file="HttpContextSetUser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK
using System;
using System.ComponentModel;
using System.Security.Claims;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.UserEvents
{
    /// <summary>
    /// The ASP.NET Core middleware integration.
    /// public static Task SignInAsync(this HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
    /// if we make it till here, it means sign in has succeeded
    /// </summary>
    [InstrumentMethod(
        AssemblyName = AssemblyName,
        TypeName = HttpContextExtensionsTypeName,
        ParameterTypeNames = ["System.Security.Claims.ClaimsPrincipal"],
        MethodName = "set_User",
        ReturnTypeName = ClrNames.Task,
        MinimumVersion = Major2,
        MaximumVersion = SupportedVersions.LatestDotNet,
        IntegrationName = nameof(IntegrationId.AspNetCore),
        InstrumentationCategory = InstrumentationCategory.AppSec)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class HttpContextSetUser
    {
        private const string Major2 = "2";
        private const string AssemblyName = "Microsoft.AspNetCore.Http";

        private const string HttpContextExtensionsTypeName = "Microsoft.AspNetCore.Http.DefaultHttpContext";

        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref ClaimsPrincipal? claimsPrincipal)
        {
            if (Security.Instance is { IsTrackUserEventsEnabled: true } security)
            {
                var tracer = Tracer.Instance;
                var scope = tracer.InternalActiveScope;
                if (instance is HttpContext httpContext && scope is { Span: Span span } && claimsPrincipal is not null)
                {
                    var foundUserId = false;
                    Func<string, string>? processPii = null;
                    string successAutoMode;
                    if (security.IsAnonUserTrackingMode)
                    {
                        processPii = UserEventsCommon.Anonymize;
                        successAutoMode = SecuritySettings.UserTrackingAnonMode;
                    }
                    else
                    {
                        successAutoMode = SecuritySettings.UserTrackingIdentMode;
                    }

                    var setTag = TaggingUtils.GetSpanSetter(span, out _);
                    var tryAddTag = TaggingUtils.GetSpanSetter(span, out _, replaceIfExists: false);
                    var secCoord = SecurityCoordinator.Get(security, span, httpContext);
                    string? userId = null;
                    foreach (var claim in claimsPrincipal.Claims)
                    {
                        if (string.IsNullOrEmpty(claim.Value))
                        {
                            continue;
                        }

                        // Authenticated user tracking focuses specifically on collecting the current user ID, rather than the user login, as the ID uniquely identifies the user within and across sessions.
                        if (claim.Type is ClaimTypes.NameIdentifier && !foundUserId)
                        {
                            foundUserId = true;
                            userId = processPii?.Invoke(claim.Value) ?? claim.Value;
                            tryAddTag(Tags.User.Id, userId);
                            setTag(Tags.AppSec.EventsUsers.InternalUserId, userId);
                            tryAddTag(Tags.AppSec.EventsUsers.CollectionMode, successAutoMode);

                            secCoord.Reporter.CollectHeaders();
                            security.SetTraceSamplingPriority(span);
                            break;
                        }
                    }

                    ISessionFeature? sessionFeatureProxy = null;
                    var sessionFeature = httpContext.Features[SecurityCoordinatorHelpers.SessionFeature];

                    if (sessionFeature is not null)
                    {
                        sessionFeatureProxy = sessionFeature.DuckCast<ISessionFeature>();
                    }

                    var result = secCoord.RunWafForUser(userSessionId: sessionFeatureProxy?.Session?.Id, userId: userId);
                    secCoord.BlockAndReport(result);

                    UserEventsCommon.RecordMetricsLoginSuccessIfNotFound(foundUserId, true);
                }
            }

            return CallTargetState.GetDefault();
        }
    }
}
#endif
