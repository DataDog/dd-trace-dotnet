// <copyright file="AspNetCoreBlockMiddlewareIntegrationEnd.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// The ASP.NET Core middleware integration.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = AssemblyName,
        TypeName = ApplicationBuilder,
        MethodName = "Build",
        ReturnTypeName = "Microsoft.AspNetCore.Http.RequestDelegate",
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
    [Browsable(false)]
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
            where TTarget : IApplicationBuilder
        {
            if (Security.Instance.Settings.Enabled)
            {
                instance.Components.Insert(0, rd => new BlockingMiddleware(rd, true).Invoke);

                var componentsCount = instance.Components.Count;
                for (var i = 2; i < componentsCount; i += 2)
                {
                    instance.Components.Insert(i, rd => new BlockingMiddleware(rd).Invoke);
                    componentsCount++;
                }

                instance.Components.Add(rd => new EndPipelineBlockingMiddleware(rd, true).Invoke);
            }

            return default;
        }
    }
}
#endif
