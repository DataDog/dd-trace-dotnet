using System;
using System.Collections;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.LogsInjection;
using Datadog.Trace.Configuration;
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
    [BenchmarkAgent5]
    [BenchmarkCategory(Constants.TracerCategory)]
    public class SerilogBenchmark
    {
        private static readonly Logger Logger;
        private static readonly Tracer LogInjectionTracer;
        private static readonly LogEvent LogEvent;

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

            LogEvent = new LogEvent(
                DateTimeOffset.Now,
                LogEventLevel.Information,
                exception: null,
                new MessageTemplate("Hello", Enumerable.Empty<MessageTemplateToken>()),
                properties: Enumerable.Empty<LogEventProperty>());
        }

        [Benchmark]
        public void EnrichedLog()
        {
            using (LogInjectionTracer.StartActive("Test"))
            {
                using (LogInjectionTracer.StartActive("Child"))
                {
                    // equivalent of auto-instrumentation
                    LoggerDispatchInstrumentation.OnMethodBegin(Logger, LogEvent);

                    Logger.Write(LogEvent);
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
