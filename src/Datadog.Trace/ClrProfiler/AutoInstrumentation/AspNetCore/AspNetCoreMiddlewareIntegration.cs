// <copyright file="AspNetCoreMiddlewareIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.AspNetCore;
#endif
using System.ComponentModel;
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
        IntegrationName = nameof(IntegrationIds.AspNetCore))]
    [InstrumentMethod(
        AssemblyName = AssemblyName,
        TypeName = InternalApplicationBuilder,
        MethodName = "Build",
        ReturnTypeName = "Microsoft.AspNetCore.Http.RequestDelegate",
        MinimumVersion = Major2,
        MaximumVersion = Major2,
        IntegrationName = nameof(IntegrationIds.AspNetCore))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AspNetCoreMiddlewareIntegration
    {
        private const string Major2 = "2";
        private const string Major3 = "3";
        private const string Major6 = "6";
        private const string AssemblyName = "Microsoft.AspNetCore.Http";

        private const string ApplicationBuilder = "Microsoft.AspNetCore.Builder.ApplicationBuilder";
        private const string InternalApplicationBuilder = "Microsoft.AspNetCore.Builder.Internal.ApplicationBuilder";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AspNetCoreMiddlewareIntegration));

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
// .NET Framework app and should be an no-op in this case
#if !NETFRAMEWORK
            if (Security.Instance.Settings.Enabled)
            {
                Log.Information("Inserting Middleware");
                BlockingMiddleware.ModifyApplicationBuilder(instance);
            }
#endif

            return default;
        }
    }
}
