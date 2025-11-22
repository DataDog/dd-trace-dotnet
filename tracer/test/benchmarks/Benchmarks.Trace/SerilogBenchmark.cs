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
        private Logger _logger;
        private Tracer _logInjectionTracer;
        private LogEvent _logEvent;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var logInjectionSettings = TracerSettings.Create(new()
            {
                { ConfigurationKeys.StartupDiagnosticLogEnabled, false },
                { ConfigurationKeys.LogsInjectionEnabled, true },
                { ConfigurationKeys.Environment, "env" },
                { ConfigurationKeys.ServiceVersion, "version" },
            });

            _logInjectionTracer = new Tracer(logInjectionSettings, new DummyAgentWriter(), null, null, null);
            Tracer.UnsafeSetTracerInstance(_logInjectionTracer);

            var formatter = new MessageTemplateTextFormatter("{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}{Properties}{NewLine}", null);

            _logger = new LoggerConfiguration()
                // Add Enrich.FromLogContext to emit Datadog properties
                .Enrich.FromLogContext()
                .WriteTo.Sink(new NullSink(formatter))
                .CreateLogger();

            _logEvent = new LogEvent(
                DateTimeOffset.Now,
                LogEventLevel.Information,
                exception: null,
                new MessageTemplate("Hello", Enumerable.Empty<MessageTemplateToken>()),
                properties: Enumerable.Empty<LogEventProperty>());

            // Warmup
            EnrichedLog();
        }

        [Benchmark]
        public void EnrichedLog()
        {
            using (_logInjectionTracer.StartActive("Test"))
            {
                using (_logInjectionTracer.StartActive("Child"))
                {
                    // equivalent of auto-instrumentation
                    LoggerDispatchInstrumentation.OnMethodBegin(_logger, _logEvent);

                    _logger.Write(_logEvent);
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
