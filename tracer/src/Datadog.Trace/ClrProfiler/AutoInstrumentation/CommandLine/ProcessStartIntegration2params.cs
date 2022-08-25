// <copyright file="ProcessStartIntegration2params.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Process
{
    /// <summary>
    /// System.Net.Http.HttpClientHandler calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
   AssemblyName = "System",
   TypeName = "System.Diagnostics.Process",
   MethodName = "Start",
   ReturnTypeName = ClrNames.Process,
   ParameterTypeNames = new[] { ClrNames.String, ClrNames.String },
   MinimumVersion = "1.0.0",
   MaximumVersion = "7.*.*",
   IntegrationName = nameof(Configuration.IntegrationId.CommandExecution))]
    public class ProcessStartIntegration2params
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="filename">file name</param>
        /// <param name="commandParams">arguments passed to the process</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(ref string filename, ref string commandParams)
        {
            return new CallTargetState(scope: ProcessStartCommon.CreateScope(filename, commandParams));
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return value</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">the return value processce</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>CallTargetReturn</returns>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
