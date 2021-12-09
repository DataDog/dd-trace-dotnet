// <copyright file="LoggerDispatchInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.LogsInjection
{
    /// <summary>
    /// LoggerFactoryScopeProvider.ForEach&lt;TState&gt; calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Serilog",
        TypeName = "Serilog.Core.Pipeline.Logger",
        MethodName = "Dispatch",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "Serilog.Events.LogEvent" },
        MinimumVersion = "1.4.214",
        MaximumVersion = "1.*.*",
        IntegrationName = "Serilog")]
    [InstrumentMethod(
        AssemblyName = "Serilog",
        TypeName = "Serilog.Core.Logger",
        MethodName = "Dispatch",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "Serilog.Events.LogEvent" },
        MinimumVersion = "2.0.0",
        MaximumVersion = "2.*.*",
        IntegrationName = "Serilog")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class LoggerDispatchInstrumentation
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TLogEvent">The type of the LogEvent</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="loggingEvent">The logging event</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TLogEvent>(TTarget instance, TLogEvent loggingEvent)
        {
            var tracer = Tracer.Instance;

            if (tracer.Settings.LogsInjectionEnabled)
            {
                var dict = loggingEvent.DuckCast<LogEventProxy>().Properties;
                AddPropertyIfAbsent(dict, CorrelationIdentifier.SerilogServiceKey, tracer.DefaultServiceName);
                AddPropertyIfAbsent(dict, CorrelationIdentifier.SerilogVersionKey, tracer.Settings.ServiceVersion);
                AddPropertyIfAbsent(dict, CorrelationIdentifier.SerilogEnvKey, tracer.Settings.Environment);

                var span = tracer.ActiveScope?.Span;
                if (span is not null)
                {
                    AddPropertyIfAbsent(dict, CorrelationIdentifier.SerilogTraceIdKey, span.TraceId.ToString());
                    AddPropertyIfAbsent(dict, CorrelationIdentifier.SerilogSpanIdKey, span.SpanId.ToString());
                }
            }

            return new CallTargetState(scope: null, state: null);

            static void AddPropertyIfAbsent(IDictionary dict, string key, string value)
            {
                if (!dict.Contains(key))
                {
                    var property = SerilogLogPropertyHelper<TTarget>.CreateScalarValue(value ?? string.Empty);
                    dict.Add(key, property);
                }
            }
        }
    }
}
