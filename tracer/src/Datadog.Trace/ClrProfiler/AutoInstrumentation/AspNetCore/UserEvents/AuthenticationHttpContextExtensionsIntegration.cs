// <copyright file="AuthenticationHttpContextExtensionsIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.ComponentModel;
using System.Linq;
using System.Security.Claims;
using Datadog.Trace.AppSec;
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

        internal static CallTargetState OnMethodBegin<TTarget>(object httpContext, string scheme, ClaimsPrincipal claimPrincipal, object authProperties)
        {
            var security = Security.Instance;
            if (security.TrackUserEvents)
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
            // https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
            var claimsToTestInSafeMode = new[] { ClaimTypes.NameIdentifier, ClaimTypes.Name, "sub" };
            if (claimsPrincipal?.Claims != null && Security.Instance is { TrackUserEvents: true } security)
            {
                var span = state.Scope.Span;
                var setTag = TaggingUtils.GetSpanSetter(span, out _);
                var tryAddTag = TaggingUtils.GetSpanSetter(span, out _, replaceIfExists: false);
                setTag(Tags.AppSec.EventsUsers.LoginEvent.SuccessTrack, "true");
                setTag(Tags.AppSec.EventsUsers.LoginEvent.SuccessAutoMode, Security.Instance.Settings.UserEventsAutomatedTracking);
                foreach (var claim in claimsPrincipal.Claims)
                {
                    if (security.IsExtendedUserTrackingEnabled)
                    {
                        switch (claim.Type)
                        {
                            case ClaimTypes.NameIdentifier or "sub":
                                tryAddTag(Tags.User.Id, claim.Value);
                                break;
                            case ClaimTypes.Email:
                                tryAddTag(Tags.User.Email, claim.Value);
                                break;
                            case ClaimTypes.Name:
                                tryAddTag(Tags.User.Name, claim.Value);
                                break;
                        }
                    }
                    else if (claimsToTestInSafeMode.Contains(claim.Type))
                    {
                        if (Guid.TryParse(claim.Value, out _))
                        {
                            tryAddTag(Tags.User.Id, claim.Value);
                            break;
                        }
                    }
                }

                security.SetTraceSamplingPriority(span);
            }

            return returnValue;
        }
    }
}
#endif
