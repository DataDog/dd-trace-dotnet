// <copyright file="LambdaOneParam.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS
{
    /// <summary>
    /// Lambda customer handler calltarget instrumentation
    /// </summary>
    public class LambdaOneParam
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LambdaOneParam));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="incomingEventOrContext">IncomingEventOrContext value</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, object incomingEventOrContext)
        {
            Console.WriteLine("OnMethodBegin - one param");
            return new CallTargetState(LambdaCommon.CreatePlaceholderScope(Tracer.Instance));
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the response, in an async scenario will be T of Task of T</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">HttpResponse message instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value</returns>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            Console.WriteLine("OnMethodEnd - one param");
            state.Scope?.Dispose();
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
