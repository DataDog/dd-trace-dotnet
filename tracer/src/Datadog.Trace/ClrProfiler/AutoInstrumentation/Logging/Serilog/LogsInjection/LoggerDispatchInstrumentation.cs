// <copyright file="LoggerDispatchInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
        // Support for property names with underscores begins with Serilog package 1.4.16 / AssemblyVersion 1.4.0.0 / commit https://github.com/serilog/serilog/commit/8e8ecb32a194ac5360502ab467ada5a683ce35ba
        MinimumVersion = "1.4.0.0",
        MaximumVersion = "1.*.*",
        IntegrationName = "Serilog")]
    [InstrumentMethod(
        AssemblyName = "Serilog",
        TypeName = "Serilog.Core.Logger",
        MethodName = "Dispatch",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "Serilog.Events.LogEvent" },
        MinimumVersion = "2.0.0",
        MaximumVersion = "4.*.*",
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
        internal static CallTargetState OnMethodBegin<TTarget, TLogEvent>(TTarget instance, TLogEvent loggingEvent)
        {
            var tracer = Tracer.Instance;

            if (tracer.Settings.LogsInjectionEnabledInternal)
            {
                var dict = loggingEvent.DuckCast<LogEventProxy>().Properties;
                AddPropertyIfAbsent(dict, CorrelationIdentifier.SerilogServiceKey, tracer.DefaultServiceName);
                AddPropertyIfAbsent(dict, CorrelationIdentifier.SerilogVersionKey, tracer.Settings.ServiceVersionInternal);
                AddPropertyIfAbsent(dict, CorrelationIdentifier.SerilogEnvKey, tracer.Settings.EnvironmentInternal);

                var spanContext = tracer.DistributedSpanContext;
                if (spanContext is not null)
                {
                    // For mismatch version support we need to keep requesting old keys.
                    var hasTraceId = spanContext.TryGetValue(SpanContext.Keys.TraceId, out string traceId) ||
                                     spanContext.TryGetValue(HttpHeaderNames.TraceId, out traceId);
                    var hasSpanId = spanContext.TryGetValue(SpanContext.Keys.ParentId, out string spanId) ||
                                    spanContext.TryGetValue(HttpHeaderNames.ParentId, out spanId);
                    if (hasTraceId && hasSpanId)
                    {
                        AddPropertyIfAbsent(dict, CorrelationIdentifier.SerilogTraceIdKey, traceId);
                        AddPropertyIfAbsent(dict, CorrelationIdentifier.SerilogSpanIdKey, spanId);
                    }
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
