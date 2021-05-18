using System.IO;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
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
        private static readonly NLog.Logger BaselineLogger;
        private static readonly NLog.Logger EnrichedLogger;

        static NLogBenchmark()
        {
            var provider = new CustomNLogLogProvider();
            provider.RegisterLayoutRenderers();

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

#if DEBUG
            var writer = System.Console.Out;
#else
            var writer = TextWriter.Null;
#endif

            var baselineConfig = new LoggingConfiguration();

            baselineConfig.AddRuleForAllLevels(new TextWriterTarget(writer)
            {
                Layout = "${longdate}|${uppercase:${level}}|${logger}|{dd.env=,dd.service=,dd.version=,dd.trace_id=,dd.span_id=}|${message}"
            });
            
            baselineConfig.LogFactory.ReconfigExistingLoggers();
            
            BaselineLogger = new LogFactory(baselineConfig).GetCurrentClassLogger();

            var enrichedConfig = new LoggingConfiguration(new LogFactory());

            enrichedConfig.AddRuleForAllLevels(new TextWriterTarget(writer)
            {
                Layout = $"${{longdate}}|${{uppercase:${{level}}}}|${{logger}}|{{dd.env=${{{CorrelationIdentifier.EnvKey}}},dd.service=${{{CorrelationIdentifier.ServiceKey}}},dd.version=${{{CorrelationIdentifier.VersionKey}}},dd.trace_id=${{{CorrelationIdentifier.TraceIdKey}}},dd.span_id=${{{CorrelationIdentifier.SpanIdKey}}}}}|${{message}}"
            });

            EnrichedLogger = new LogFactory(enrichedConfig).GetCurrentClassLogger();
        }

        [Benchmark(Baseline = true)]
        public void Log()
        {
            using (BaselineTracer.StartActive("Test"))
            {
                using (BaselineTracer.StartActive("Child"))
                {
                    BaselineLogger.Info("Hello");
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
                    EnrichedLogger.Info("Hello");
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
