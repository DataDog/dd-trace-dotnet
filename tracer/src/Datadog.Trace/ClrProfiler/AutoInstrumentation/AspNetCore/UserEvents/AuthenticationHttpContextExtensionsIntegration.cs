// <copyright file="AuthenticationHttpContextExtensionsIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Claims;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

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
        ParameterTypeNames = new[] { "Microsoft.AspNetCore.Http.HttpContext", ClrNames.String, "System.Security.Claims.ClaimsPrincipal", "Microsoft.AspNetCore.Authentication.AuthenticationProperties" },
        MethodName = "SignInAsync",
        ReturnTypeName = ClrNames.Task,
        MinimumVersion = Major2,
        MaximumVersion = "8",
        IntegrationName = nameof(IntegrationId.AspNetCore))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AuthenticationHttpContextExtensionsIntegration
    {
        private const string Major2 = "2";
        private const string AssemblyName = "Microsoft.AspNetCore.Authentication.Abstractions";

        private const string HttpContextExtensionsTypeName = "Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions";

        // https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        private static readonly HashSet<string> ClaimsToTest = new HashSet<string>
        {
            ClaimTypes.NameIdentifier, ClaimTypes.Name, "sub", ClaimTypes.Email,  ClaimTypes.Name
        };

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
            if (claimsPrincipal?.Claims != null && Security.Instance is { IsTrackUserEventsEnabled: true } security)
            {
                var span = state.Scope.Span;
                var setTag = TaggingUtils.GetSpanSetter(span, out _);
                var tryAddTag = TaggingUtils.GetSpanSetter(span, out _, replaceIfExists: false);

                var foundUserId = false;

                foreach (var claim in claimsPrincipal.Claims)
                {
                    if (ClaimsToTest.Contains(claim.Type))
                    {
                        if (security.IsAnonUserTrackingMode)
                        {
                            var anonId = UserEventsCommon.GetAnonId(claim.Value);
                            tryAddTag(Tags.User.Id, anonId);
                            setTag(Tags.AppSec.EventsUsers.LoginEvent.SuccessAutoMode, SecuritySettings.UserTrackingAnonMode);
                        }
                        else
                        {
                            tryAddTag(Tags.User.Id, claim.Value);
                            setTag(Tags.AppSec.EventsUsers.LoginEvent.SuccessAutoMode, SecuritySettings.UserTrackingIdentMode);
                        }

                        foundUserId = true;

                        break;
                    }
                }

                if (foundUserId)
                {
                    security.SetTraceSamplingPriority(span);
                    setTag(Tags.AppSec.EventsUsers.LoginEvent.SuccessTrack, "true");
                }
                else
                {
                    TelemetryFactory.Metrics.RecordCountMissingUserId(MetricTags.AuthenticationFramework.AspNetCoreIdentity);
                }
            }

            return returnValue;
        }
    }
}
#endif
