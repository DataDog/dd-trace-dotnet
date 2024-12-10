// <copyright file="HttpContextSetUser.cs" company="Datadog">
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
    [InstrumentMethod(
        AssemblyName = AssemblyName,
        TypeName = HttpContextExtensionsTypeName,
        ParameterTypeNames = ["System.Security.Claims.ClaimsPrincipal"],
        MethodName = "set_User",
        ReturnTypeName = ClrNames.Task,
        MinimumVersion = Major2,
        CallTargetIntegrationKind = CallTargetKind.Derived,
        MaximumVersion = SupportedVersions.LatestDotNet,
        IntegrationName = nameof(IntegrationId.AspNetCore),
        InstrumentationCategory = InstrumentationCategory.AppSec)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class HttpContextSetUser
    {
        private const string Major2 = "2";
        private const string AssemblyName = "Microsoft.AspNetCore.Http.Abstractions";

        private const string HttpContextExtensionsTypeName = "Microsoft.AspNetCore.Http.HttpContext";

        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, object claimPrincipal)
        {
            var security = Security.Instance;
            if (security.IsTrackUserEventsEnabled)
            {
                if (instance is HttpContext httpContext)
                {
                    if (httpContext.Features.Get<ISessionFeature>() is { } sessionFeature)
                    {
                        var sessionKey = sessionFeature.Session.Id;
                    }
                }
            }

            return CallTargetState.GetDefault();
        }
    }
}
#endif
