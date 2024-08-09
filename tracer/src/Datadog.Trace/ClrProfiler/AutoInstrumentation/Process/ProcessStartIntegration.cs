// <copyright file="ProcessStartIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Iast;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Process
{
    /// <summary>
    /// System.Diagnostics.Process calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
       AssemblyNames = new[] { "System", "System.Diagnostics.Process" },
       TypeName = "System.Diagnostics.Process",
       MethodName = "Start",
       ReturnTypeName = ClrNames.Process,
       MinimumVersion = "1.0.0",
       MaximumVersion = "8.*.*",
       IntegrationName = nameof(Configuration.IntegrationId.Process))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ProcessStartIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            // Make sure we're not starting a "do not trace" process
            if (instance is System.Diagnostics.Process { StartInfo: var startInfo }
                && ProcessHelpers.ShouldTraceProcessStart())
            {
                VulnerabilitiesModule.OnCommandInjection(startInfo);
                return new CallTargetState(scope: ProcessStartCommon.CreateScope(startInfo));
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return value</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">the return value</param>
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
