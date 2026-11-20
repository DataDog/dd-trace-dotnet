using System.IO;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.LogsInjection;
using Datadog.Trace.Configuration;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    [BenchmarkCategory(Constants.TracerCategory, Constants.RunOnPrs, Constants.RunOnMaster)]
    public class NLogBenchmark
    {
        private NLog.Logger _logger;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var settings = TracerHelper.DefaultConfig;
            settings[ConfigurationKeys.LogsInjectionEnabled] = true;
            settings[ConfigurationKeys.Environment] = "env";
            settings[ConfigurationKeys.ServiceVersion] = "version";
            TracerHelper.SetGlobalTracer(settings);

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
            _logger = LogManager.GetCurrentClassLogger();

            // Run the automatic instrumentation initialization code once outside of the microbenchmark
            _ = DiagnosticContextHelper.Cache<NLog.Logger>.Mdlc;
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            LogManager.Shutdown();
            TracerHelper.CleanupGlobalTracer();
        }

        [Benchmark]
        public void EnrichedLog()
        {
            using (Tracer.Instance.StartActive("Test"))
            {
                using (Tracer.Instance.StartActive("Child"))
                {
                    // None of the arguments are used directly
                    // First arg is a marker type, so needs to be an NLog type
                    // Remainder can be any object
                    var callTargetState = LoggerImplWriteIntegrationV5.OnMethodBegin(_logger, typeof(NLog.Logger), _logger, _logger, _logger);

                    _logger.Info("Hello");

                    LoggerImplWriteIntegrationV5.OnMethodEnd(_logger, exception: null, callTargetState);
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
