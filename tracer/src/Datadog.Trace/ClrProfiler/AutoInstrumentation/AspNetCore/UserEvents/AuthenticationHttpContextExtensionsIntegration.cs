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
                return new CallTargetState(scope, claimPrincipal);
            }

            return CallTargetState.GetDefault();
        }

        internal static object OnAsyncMethodEnd<TTarget>(object returnValue, Exception exception, in CallTargetState state)
        {
            var claimsPrincipal = state.State as ClaimsPrincipal;
            if (claimsPrincipal?.Claims is not null && Security.Instance is { IsTrackUserEventsEnabled: true } security && state.Scope is { } scope)
            {
                var span = scope.Span;
                var foundUserId = false;
                var foundLogin = false;
                Func<string, string> processPii;
                string successAutoMode;
                if (security.IsAnonUserTrackingMode)
                {
                    processPii = UserEventsCommon.Anonymize;
                    successAutoMode = SecuritySettings.UserTrackingAnonMode;
                }
                else
                {
                    processPii = val => val;
                    successAutoMode = SecuritySettings.UserTrackingIdentMode;
                }

                var setTag = TaggingUtils.GetSpanSetter(span, out _);
                var tryAddTag = TaggingUtils.GetSpanSetter(span, out _, replaceIfExists: false);
                foreach (var claim in claimsPrincipal.Claims)
                {
                    if (string.IsNullOrEmpty(claim.Value))
                    {
                        continue;
                    }

                    if (claim.Type is ClaimTypes.NameIdentifier && !foundUserId)
                    {
                        foundUserId = true;
                        var userId = processPii(claim.Value);
                        tryAddTag(Tags.User.Id, userId);
                        setTag(Tags.AppSec.EventsUsers.InternalUserId, userId);
                    }
                    else if (LoginsClaimsToTest.Contains(claim.Type) && !foundLogin)
                    {
                        foundLogin = true;
                        var login = processPii(claim.Value);
                        setTag(Tags.AppSec.EventsUsers.InternalLogin, login);
                        tryAddTag(Tags.AppSec.EventsUsers.LoginEvent.SuccessLogin, login);
                    }

                    if (foundLogin && foundUserId)
                    {
                        break;
                    }
                }

                if (foundUserId || foundLogin)
                {
                    security.SetTraceSamplingPriority(span);
                    setTag(Tags.AppSec.EventsUsers.LoginEvent.SuccessTrack, Tags.AppSec.EventsUsers.True);
                    setTag(Tags.AppSec.EventsUsers.LoginEvent.SuccessAutoMode, successAutoMode);
                }

                UserEventsCommon.RecordMetricsLoginSuccessIfNotFound(foundUserId, foundLogin);
                SecurityCoordinator.CollectHeaders(span);
            }

            return returnValue;
        }
    }
}
#endif
