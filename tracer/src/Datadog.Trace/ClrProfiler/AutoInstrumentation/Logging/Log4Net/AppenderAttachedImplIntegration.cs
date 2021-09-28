// <copyright file="AppenderAttachedImplIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Log4Net
{
    /// <summary>
    /// LoggerFactoryScopeProvider.ForEach&lt;TState&gt; calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "log4net",
        TypeName = "log4net.Util.AppenderAttachedImpl",
        MethodName = "AppendLoopOnAppenders",
        ReturnTypeName = ClrNames.Int32,
        ParameterTypeNames = new[] { "log4net.Core.LoggingEvent" },
        MinimumVersion = "2.0.0",
        MaximumVersion = "2.*.*",
        IntegrationName = "Log4Net")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class AppenderAttachedImplIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TLoggingEvent">The type of the logging event</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="loggingEvent">The logging event</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TLoggingEvent>(TTarget instance, TLoggingEvent loggingEvent)
            where TLoggingEvent : ILoggingEvent
        {
            var tracer = Tracer.Instance;

            if (tracer.Settings.LogsInjectionEnabled &&
                !loggingEvent.Properties.Contains(CorrelationIdentifier.ServiceKey))
            {
                var span = tracer.ActiveScope?.Span;
                loggingEvent.Properties[CorrelationIdentifier.ServiceKey] = CorrelationIdentifier.Service;
                loggingEvent.Properties[CorrelationIdentifier.VersionKey] = CorrelationIdentifier.Version;
                loggingEvent.Properties[CorrelationIdentifier.EnvKey] = CorrelationIdentifier.Env;

                if (span is not null)
                {
                    loggingEvent.Properties[CorrelationIdentifier.TraceIdKey] = CorrelationIdentifier.TraceId;
                    loggingEvent.Properties[CorrelationIdentifier.SpanIdKey] = CorrelationIdentifier.SpanId;
                }
            }

            return new CallTargetState(scope: null, state: null);
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return value</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Original return value</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        {
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
