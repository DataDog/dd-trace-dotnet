using System.IO;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.LogProviders;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    public class NLogBenchmark
    {
        private static readonly Tracer BaselineTracer;
        private static readonly Tracer LogInjectionTracer;
        private static readonly NLog.Logger Logger;

        static NLogBenchmark()
        {
            LogProvider.SetCurrentLogProvider(new NLogLogProvider());

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

            var config = new LoggingConfiguration();

#if DEBUG
            var writer = System.Console.Out;
#else
            var writer = TextWriter.Null;
#endif

            var target = new TextWriterTarget(writer)
            {
                Layout = "${longdate}|${uppercase:${level}}|${logger}|{dd.env=${mdlc:item=dd.env},dd.service=${mdlc:item=dd.service},dd.version=${mdlc:item=dd.version},dd.trace_id=${mdlc:item=dd.trace_id},dd.span_id=${mdlc:item=dd.span_id}}|${message}"
            };

            config.AddRuleForAllLevels(target);

            LogManager.Configuration = config;
            Logger = LogManager.GetCurrentClassLogger();
        }

        [Benchmark(Baseline = true)]
        public void Log()
        {
            using (BaselineTracer.StartActive("Test"))
            {
                using (BaselineTracer.StartActive("Child"))
                {
                    Logger.Info("Hello");
                }
            }
        }

        [Benchmark]
        public void EnrichedLog()
        {
            using (LogInjectionTracer.StartActive("Test"))
            {
                using (LogInjectionTracer.StartActive("Child"))
                {
                    Logger.Info("Hello");
                }
            }
        }

        private class TextWriterTarget : TargetWithLayout
        {
            private readonly TextWriter _writer;

            public TextWriterTarget(TextWriter textWriter)
            {
                _writer = textWriter;
            }

            protected override void Write(LogEventInfo logEvent)
            {
                _writer.WriteLine(RenderLogEvent(Layout, logEvent));
            }
        }
    }
}
