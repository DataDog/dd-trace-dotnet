using System.IO;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.Configuration;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    public class SerilogBenchmark
    {
        private static readonly Logger EnrichedLogger;
        private static readonly Logger Logger;
        private static readonly Tracer LogInjectionTracer;
        private static readonly Tracer BaselineTracer;

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
            Tracer.Instance = LogInjectionTracer;

            var baselineSettings = new TracerSettings
            {
                StartupDiagnosticLogEnabled = false,
                LogsInjectionEnabled = false,
                Environment = "env",
                ServiceVersion = "version"
            };

            BaselineTracer = new Tracer(baselineSettings, new DummyAgentWriter(), null, null, null);

            EnrichedLogger = new LoggerConfiguration()
                // Add Enrich.FromLogContext to emit Datadog properties
                .Enrich.FromLogContext()
                .WriteTo.Sink(new NullSink())
                .CreateLogger();

            Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Sink(new NullSink())
                .CreateLogger();
        }

        [Benchmark(Baseline = true)]
        public void Log()
        {
            using (BaselineTracer.StartActive("Test"))
            {
                Logger.Information("Hello");
            }
        }

        [Benchmark]
        public void EnrichedLog()
        {
            using (LogInjectionTracer.StartActive("Test"))
            {
                EnrichedLogger.Information("Hello");
            }
        }

        private class NullSink : ILogEventSink
        {
            public void Emit(LogEvent logEvent)
            {
                logEvent.RenderMessage(TextWriter.Null);
            }
        }
    }
}
