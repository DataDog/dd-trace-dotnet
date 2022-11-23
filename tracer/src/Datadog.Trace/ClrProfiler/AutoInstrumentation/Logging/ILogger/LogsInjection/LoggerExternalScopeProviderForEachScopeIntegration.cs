// <copyright file="LoggerExternalScopeProviderForEachScopeIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger
{
    /// <summary>
    /// LoggerExternalScopeProvider.ForEach&lt;TState&gt; calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Microsoft.Extensions.Logging.Abstractions",
        TypeName = "Microsoft.Extensions.Logging.LoggerExternalScopeProvider",
        MethodName = "ForEachScope",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "System.Action`2[System.Object,!!0]", "!!0" },
        MinimumVersion = "2.0.0",
        MaximumVersion = "7.*.*",
        IntegrationName = LoggerIntegrationCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class LoggerExternalScopeProviderForEachScopeIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TAction">The type of the action</typeparam>
        /// <typeparam name="TState">The type of the state</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="callback">The callback to be invoked per scope</param>
        /// <param name="state">The state to pass to the callback</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TAction, TState>(TTarget instance, TAction callback, TState state)
        {
            LoggerIntegrationCommon.AddScope(Tracer.Instance, callback, state);
            return new CallTargetState(scope: null, state: null);
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
        {
            return CallTargetReturn.GetDefault();
        }
    }
}
