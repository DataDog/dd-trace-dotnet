// <copyright file="AspNetCoreMiddlewareIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.AspNetCore;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

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
        MinimumVersion = MinimumVersion,
        MaximumVersion = MaximumVersion,
        IntegrationName = nameof(IntegrationIds.AspNetCore))]
    public static class AspNetCoreMiddlewareIntegration
    {
        private const string HttpContextKey = "__Datadog.Trace.ClrProfiler.Integrations.AspNetCoreMiddlewareIntegration";
        private const string MinimumVersion = "2";
        private const string MaximumVersion = "6";
        private const string AssemblyName = "Microsoft.AspNetCore.Http";

        private const string ApplicationBuilder = "Microsoft.AspNetCore.Builder.ApplicationBuilder";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            if (Security.Instance.Enabled)
            {
                BlockingMiddleware.ModifyApplicationBuilder(instance);
            }

            return default;
        }
    }
}
#endif
