// <copyright file="AspNetCoreBlockMiddlewareIntegrationEnd.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
#endif
using System.Diagnostics;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// The ASP.NET Core middleware integration.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = AssemblyName,
        TypeName = ApplicationBuilder,
        MethodName = "Build",
        ReturnTypeName = ClrNames.Void,
        MinimumVersion = Major3,
        MaximumVersion = Major6,
        IntegrationName = nameof(IntegrationId.AspNetCore))]
    [InstrumentMethod(
        AssemblyName = AssemblyName,
        TypeName = InternalApplicationBuilder,
        MethodName = "Build",
        ReturnTypeName = "Microsoft.AspNetCore.Http.RequestDelegate",
        MinimumVersion = Major2,
        MaximumVersion = Major2,
        IntegrationName = nameof(IntegrationId.AspNetCore))]
    public static class AspNetCoreBlockMiddlewareIntegrationEnd
    {
        private const string Major2 = "2";
        private const string Major3 = "3";
        private const string Major6 = "6";
        private const string AssemblyName = "Microsoft.AspNetCore.Http";

        private const string ApplicationBuilder = "Microsoft.AspNetCore.Builder.ApplicationBuilder";
        private const string InternalApplicationBuilder = "Microsoft.AspNetCore.Builder.Internal.ApplicationBuilder";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
// AppSec doesn't support Asp.Net Core on .NET Framework
// but this class is needed as it could be called by a
// .NET Framewok app and should be an no-op in this case
#if !NETFRAMEWORK
            if (Security.Instance.Settings.Enabled)
            {
                var appb = (IApplicationBuilder)instance;
                if (new StackTrace().GetFrame(6)?.GetMethod()?.DeclaringType?.Name == "AsyncMethodBuilderCore")
                {
                    appb.MapWhen(context => context.Items["block"] is true, AspNetCoreBlockMiddlewareIntegration.HandleBranch);
                }
            }
#endif

            return default;
        }
    }
}
