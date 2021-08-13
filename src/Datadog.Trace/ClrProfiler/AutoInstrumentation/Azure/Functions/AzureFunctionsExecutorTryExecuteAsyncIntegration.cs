// <copyright file="AzureFunctionsExecutorTryExecuteAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions
{
    /// <summary>
    /// Azure Function calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Microsoft.Azure.WebJobs.Host",
        TypeName = "Microsoft.Azure.WebJobs.Host.Executors.FunctionExecutor",
        MethodName = "TryExecuteAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.Azure.WebJobs.Host.Executors.IDelayedException]",
        ParameterTypeNames = new[] { "Microsoft.Azure.WebJobs.Host.Executors.IFunctionInstance", ClrNames.CancellationToken },
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = IntegrationName)]
    public class AzureFunctionsExecutorTryExecuteAsyncIntegration
    {
        private const string IntegrationName = nameof(IntegrationIds.AzureFunctions);

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TFunction">Type of the invoker</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="functionInstance">First argument</param>
        /// <param name="cancellationToken">Second argument</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TFunction>(TTarget instance, TFunction functionInstance, CancellationToken cancellationToken)
            where TTarget : IFunctionInvoker
            where TFunction : IFunctionInstance
        {
            return AzureFunctionsCommon.OnMethodBegin(instance, functionInstance);
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return value, in an async scenario will be T of Task of T</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Return value instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        {
            state.Scope?.DisposeWithException(exception);
            return returnValue;
        }
    }
}
