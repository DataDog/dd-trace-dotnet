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
    [BenchmarkCategory(Constants.TracerCategory, Constants.RunOnPrs, Constants.RunOnMaster)]
    public class SerilogBenchmark
    {
        private Logger _logger;
        private LogEvent _logEvent;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var settings = TracerHelper.DefaultConfig;
            settings[ConfigurationKeys.LogsInjectionEnabled] = true;
            settings[ConfigurationKeys.Environment] = "env";
            settings[ConfigurationKeys.ServiceVersion] = "version";
            TracerHelper.SetGlobalTracer(settings);

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

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _logger.Dispose();
            TracerHelper.CleanupGlobalTracer();
        }

        [Benchmark]
        public void EnrichedLog()
        {
            using (Tracer.Instance.StartActive("Test"))
            {
                using (Tracer.Instance.StartActive("Child"))
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
