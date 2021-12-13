using System;
using System.Collections;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.LogsInjection;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Parsing;
using Logger = Serilog.Core.Logger;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    public class SerilogBenchmark
    {
        private static readonly Logger Logger;
        private static readonly Tracer LogInjectionTracer;

        [Params(1, 10, 100)]
        public static int N { get; set; }

        static SerilogBenchmark()
        {
            var logInjectionSettings = new TracerSettings
            {
                StartupDiagnosticLogEnabled = false,
                LogsInjectionEnabled = true,
                Environment = "env",
                ServiceVersion = "version"
            };

            LogInjectionTracer = new Tracer(logInjectionSettings, new DummyAgentWriter(), null, null, null);
            Tracer.UnsafeSetTracerInstance(LogInjectionTracer);

            var formatter = new MessageTemplateTextFormatter("{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}{Properties}{NewLine}", null);

            Logger = new LoggerConfiguration()
                // Add Enrich.FromLogContext to emit Datadog properties
                .Enrich.FromLogContext()
                .WriteTo.Sink(new NullSink(formatter))
                .CreateLogger();
        }

        [Benchmark]
        public void EnrichedLog()
        {
            using (LogInjectionTracer.StartActive("Test"))
            {
                using (LogInjectionTracer.StartActive("Child"))
                {
                    for (int i = 0; i < N; i++)
                    {
                        var logEvent = new LogEvent(
                            DateTimeOffset.Now,
                            LogEventLevel.Information,
                            exception: null,
                            new MessageTemplate("Hello", Enumerable.Empty<MessageTemplateToken>()),
                            properties: Enumerable.Empty<LogEventProperty>());

                        // equivalent of auto-instrumentation
                        var tracer = LogInjectionTracer;
                        var dict = logEvent.DuckCast<LogEventProxy>().Properties;
                        AddPropertyIfAbsent(dict, CorrelationIdentifier.SerilogServiceKey, tracer.DefaultServiceName);
                        AddPropertyIfAbsent(dict, CorrelationIdentifier.SerilogVersionKey, tracer.Settings.ServiceVersion);
                        AddPropertyIfAbsent(dict, CorrelationIdentifier.SerilogEnvKey, tracer.Settings.Environment);

                        var span = tracer.ActiveScope?.Span;
                        if (span is not null)
                        {
                            AddPropertyIfAbsent(dict, CorrelationIdentifier.SerilogTraceIdKey, span.TraceId.ToString());
                            AddPropertyIfAbsent(dict, CorrelationIdentifier.SerilogSpanIdKey, span.SpanId.ToString());
                        }

                        Logger.Write(logEvent);
                    }
                }
            }

            static void AddPropertyIfAbsent(IDictionary dict, string key, string value)
            {
                if (!dict.Contains(key))
                {
                    var property = SerilogLogPropertyHelper<LogEventProperty>.CreateScalarValue(value ?? string.Empty);
                    dict.Add(key, property);
                }
            }
        }

        private class NullSink : ILogEventSink
        {
            private readonly MessageTemplateTextFormatter _formatter;

            public NullSink(MessageTemplateTextFormatter formatter)
            {
                _formatter = formatter;
            }

            public void Emit(LogEvent logEvent)
            {
#if DEBUG
                _formatter.Format(logEvent, System.Console.Out);
#else
                _formatter.Format(logEvent, System.IO.TextWriter.Null);
#endif
            }
        }
    }
}
