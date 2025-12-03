// <copyright file="AuthenticationHttpContextExtensionsIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Claims;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Microsoft.AspNetCore.Http;

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
        ParameterTypeNames = ["Microsoft.AspNetCore.Http.HttpContext", ClrNames.String, "System.Security.Claims.ClaimsPrincipal", "Microsoft.AspNetCore.Authentication.AuthenticationProperties"],
        MethodName = "SignInAsync",
        ReturnTypeName = ClrNames.Task,
        MinimumVersion = Major2,
        MaximumVersion = SupportedVersions.LatestDotNet,
        IntegrationName = nameof(IntegrationId.AspNetCore))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AuthenticationHttpContextExtensionsIntegration
    {
        private const string Major2 = "2";
        private const string AssemblyName = "Microsoft.AspNetCore.Authentication.Abstractions";

        private const string HttpContextExtensionsTypeName = "Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions";

        // https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        private static readonly HashSet<string> LoginsClaimsToTest =
        [
            ClaimTypes.Name,
            ClaimTypes.Email,
            "sub",
        ];

        internal static CallTargetState OnMethodBegin<TTarget>(object httpContext, string scheme, ClaimsPrincipal claimPrincipal, object authProperties)
        {
            var security = Security.Instance;
            if (security.IsTrackUserEventsEnabled)
            {
                var tracer = Tracer.Instance;
                var scope = tracer.InternalActiveScope;
                return new CallTargetState(scope, new ClaimsAndHttpContext(httpContext as HttpContext, claimPrincipal));
            }

            return CallTargetState.GetDefault();
        }

        internal static object OnAsyncMethodEnd<TTarget>(TTarget instance, object returnValue, Exception exception, in CallTargetState state)
        {
            if (state.State is ClaimsAndHttpContext stateTuple
             && Security.Instance is { IsTrackUserEventsEnabled: true } security
             && state.Scope is { Span: Span span })
            {
                string? userId = null;
                string? userLogin = null;
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
                foreach (var claim in stateTuple.ClaimsPrincipal.Claims)
                {
                    if (string.IsNullOrEmpty(claim.Value))
                    {
                        continue;
                    }

                    if (claim.Type is ClaimTypes.NameIdentifier && userId is null)
                    {
                        userId = processPii?.Invoke(claim.Value) ?? claim.Value;
                        tryAddTag(Tags.User.Id, userId);
                        setTag(Tags.AppSec.EventsUsers.InternalUserId, userId);
                    }
                    else if (LoginsClaimsToTest.Contains(claim.Type) && userLogin is null)
                    {
                        userLogin = processPii?.Invoke(claim.Value) ?? claim.Value;
                        setTag(Tags.AppSec.EventsUsers.InternalLogin, userLogin);
                        tryAddTag(Tags.AppSec.EventsUsers.LoginEvent.SuccessLogin, userLogin);
                    }

                    if (userId is not null && userLogin is not null)
                    {
                        break;
                    }
                }

                var foundUserId = userId is not null;
                var foundLogin = userLogin is not null;
                UserEventsCommon.RecordMetricsLoginSuccessIfNotFound(foundUserId, foundLogin);
                security.SetTraceSamplingPriority(span);
                setTag(Tags.AppSec.EventsUsers.LoginEvent.SuccessTrack, Tags.AppSec.EventsUsers.True);
                setTag(Tags.AppSec.EventsUsers.LoginEvent.SuccessAutoMode, successAutoMode);

                if (stateTuple.HttpContext is { } httpContext)
                {
                    var secCoordinator = SecurityCoordinator.Get(security, span, httpContext);
                    secCoordinator.Reporter.CollectHeaders();
                    if (userId is not null || userLogin is not null)
                    {
                        // if the current collection mode is anonymization, the ID must be provided after anonymization, instead of the original one.
                        var result = secCoordinator.RunWafForUser(userId: userId, userLogin: userLogin, otherTags: new() { { AddressesConstants.UserBusinessLoginSuccess, string.Empty } });
                        secCoordinator.BlockAndReport(result);
                    }
                }
            }

            return returnValue;
        }

        private record ClaimsAndHttpContext(HttpContext? HttpContext, ClaimsPrincipal ClaimsPrincipal);
    }
}
#endif
