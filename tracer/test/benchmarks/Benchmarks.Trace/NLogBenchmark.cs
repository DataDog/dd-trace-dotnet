using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.LogsInjection;
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
        private static readonly Tracer LogInjectionTracer;
        private static readonly NLog.Logger Logger;

        static NLogBenchmark()
        {
            LogProvider.SetCurrentLogProvider(new NoOpNLogLogProvider());

            var logInjectionSettings = new TracerSettings
            {
                StartupDiagnosticLogEnabled = false,
                LogsInjectionEnabled = true,
                Environment = "env",
                ServiceVersion = "version"
            };

            LogInjectionTracer = new Tracer(logInjectionSettings, new DummyAgentWriter(), null, null, null);
            Tracer.UnsafeSetTracerInstance(LogInjectionTracer);

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

            // Run the automatic instrumentation initialization code once outside of the microbenchmark
            _ = DiagnosticContextHelper.Cache<NLog.Logger>.Mdlc;
        }

        [Benchmark]
        public void EnrichedLog()
        {
            using (LogInjectionTracer.StartActive("Test"))
            {
                using (LogInjectionTracer.StartActive("Child"))
                {
                    object state = null;

                    if (DiagnosticContextHelper.Cache<NLog.Logger>.Mdlc is { } mdlc)
                    {
                        state = DiagnosticContextHelper.SetMdlcState(mdlc, LogInjectionTracer);
                    }
                    else if (DiagnosticContextHelper.Cache<NLog.Logger>.Mdc is { } mdc)
                    {
                        state = DiagnosticContextHelper.SetMdcState(mdc, LogInjectionTracer);
                    }

                    Logger.Info("Hello");

                    if (state is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    else if (state is bool removeTraceIds && DiagnosticContextHelper.Cache<NLog.Logger>.Mdc is { } mdc2)
                    {
                        DiagnosticContextHelper.RemoveMdcState(mdc2, removeTraceIds);
                    }
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
