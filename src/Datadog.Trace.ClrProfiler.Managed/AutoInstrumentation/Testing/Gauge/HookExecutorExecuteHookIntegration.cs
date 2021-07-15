// <copyright file="HookExecutorExecuteHookIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.Gauge
{
    /// <summary>
    /// Gauge.Dotnet.HookExecutor.ExecuteHook calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Gauge.Dotnet",
        TypeName = "Gauge.Dotnet.HookExecutor",
        MethodName = "ExecuteHook",
        ParameterTypeNames = new[] { "System.Reflection.MethodInfo", "_" },
        ReturnTypeName = ClrNames.Void,
        MinimumVersion = "0.4.0",
        MaximumVersion = "0.5.0",
        IntegrationName = IntegrationName)]
    public static class HookExecutorExecuteHookIntegration
    {
        internal const string IntegrationName = "Gauge";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HookExecutorExecuteHookIntegration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="methodInfo">MethodInfo instance</param>
        /// <param name="objects">Object array instance</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance, MethodInfo methodInfo, object[] objects)
        {
            if (objects?.Length == 1)
            {
                if (objects[0].TryDuckCast<IExecutionContext>(out var executionContext))
                {
                    HooksHandlers.ExecutionContext = executionContext;
                }
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, CallTargetState state)
        {
            return CallTargetReturn.GetDefault();
        }
    }
}
