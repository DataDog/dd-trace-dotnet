// <copyright file="AppenderAttachedImplIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging;
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
        MinimumVersion = "1.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = "Log4Net")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class AppenderAttachedImplIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TLoggingEvent">The type of the logging event</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="loggingEvent">The logging event</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TLoggingEvent>(TTarget instance, TLoggingEvent loggingEvent)
            where TLoggingEvent : ILoggingEvent
        {
            if (loggingEvent?.Instance is null)
            {
                return CallTargetState.GetDefault();
            }

            var tracer = Tracer.Instance;

            var mutableSettings = tracer.CurrentTraceSettings.Settings;
            var properties = loggingEvent.Properties;
            if (mutableSettings.LogsInjectionEnabled && properties != null && !properties.Contains(CorrelationIdentifier.ServiceKey))
            {
                properties[CorrelationIdentifier.ServiceKey] = mutableSettings.DefaultServiceName;
                properties[CorrelationIdentifier.VersionKey] = mutableSettings.ServiceVersion ?? string.Empty;
                properties[CorrelationIdentifier.EnvKey] = mutableSettings.Environment ?? string.Empty;

                if (tracer.DistributedSpanContext is { } context &&
                    LogContext.TryGetValues(context, out var traceId, out var spanId, tracer.Settings.TraceId128BitLoggingEnabled))
                {
                    properties[CorrelationIdentifier.TraceIdKey] = traceId;
                    properties[CorrelationIdentifier.SpanIdKey] = spanId;
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
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
