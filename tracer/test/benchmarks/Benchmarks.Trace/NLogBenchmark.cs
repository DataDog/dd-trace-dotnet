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
    [BenchmarkAgent3]
    [BenchmarkCategory(Constants.TracerCategory)]
    public class NLogBenchmark
    {
        private Tracer _logInjectionTracer;
        private NLog.Logger _logger;

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

        [Benchmark]
        public void EnrichedLog()
        {
            using (_logInjectionTracer.StartActive("Test"))
            {
                using (_logInjectionTracer.StartActive("Child"))
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
