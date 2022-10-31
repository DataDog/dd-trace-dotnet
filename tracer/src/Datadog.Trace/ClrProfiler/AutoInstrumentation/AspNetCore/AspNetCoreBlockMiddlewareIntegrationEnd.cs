// <copyright file="AspNetCoreBlockMiddlewareIntegrationEnd.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System.Diagnostics;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Microsoft.AspNetCore.Builder;

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
            if (Security.Instance.Settings.Enabled)
            {
                var appb = (IApplicationBuilder)instance;
                // make sure this is the very last call to build() as build can be called *many* times before the last one.
                // theoretically, we should have 1. onmethodbegin, 2. CallTarget.Handlers.Beginmethodhandler, 3. CalltargetInvoker.BeginMethod 4.ApplicationBuilder.Build, 5.GenericWebHostService.StartAsync, 6.AsyncMethodBuilderCore.Start<GenericWebHostService>, when comes into play, that's last call to ApplicationBuilder.
                // todo: make more robust
                var frame = new StackTrace().GetFrame(6);
                if (frame?.GetMethod() is { Name: "Start", DeclaringType.Name: "AsyncMethodBuilderCore" })
                {
                    appb.MapWhen(context => context.Items["block"] is true, AspNetCoreBlockMiddlewareIntegration.HandleBranch);
                }
            }

            return default;
        }
    }
}

#endif
