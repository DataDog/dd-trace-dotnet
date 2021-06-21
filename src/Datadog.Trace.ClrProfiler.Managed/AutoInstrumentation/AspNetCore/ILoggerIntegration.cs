// <copyright file="ILoggerIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.AspNetCore;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// Microsoft.AspNetCore.Hosting.Internal.HostingApplication.ProcessRequestAsync calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Microsoft.AspNetCore.Hosting",
        TypeName = "Microsoft.AspNetCore.Hosting.Internal.HostingApplication",
        MethodName = "ProcessRequestAsync",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { "Context" },
        MinimumVersion = "2.0.0",
        MaximumVersion = "2.*.*",
        IntegrationName = IntegrationName)]
    [InstrumentMethod(
        AssemblyName = "Microsoft.AspNetCore.Hosting",
        TypeName = "Microsoft.AspNetCore.Hosting.HostingApplication", // different namespace
        MethodName = "ProcessRequestAsync",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { "Context" },
        MinimumVersion = "3.0.0",
        MaximumVersion = "5.*.*",
        IntegrationName = IntegrationName)]
    public class ILoggerIntegration
    {
        private const string IntegrationName = nameof(IntegrationIds.AspNetCore);

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TContext">Type of the controller context</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="context">The context of the request</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext context)
            where TTarget : IHostingApplication
        {
            IDisposable disposable = null;
            if (Tracer.Instance.Settings.LogsInjectionEnabled)
            {
                var logger = instance.Diagnostics.Logger;
                disposable = logger.BeginScope(new DatadogLoggingScope());
            }

            return new CallTargetState(scope: null, state: disposable);
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResponse">Type of the response, in an async scenario will be T of Task of T</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="responseMessage">HttpResponse message instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        public static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse responseMessage, Exception exception, CallTargetState state)
        {
            if (state.State is IDisposable disposable)
            {
                disposable.Dispose();
            }

            return responseMessage;
        }
    }
}
