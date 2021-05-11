using System.IO;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.LogProviders;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    public class Log4netBenchmark
    {
        private static readonly Tracer BaselineTracer;
        private static readonly Tracer LogInjectionTracer;
        private static readonly log4net.ILog Logger;

        static Log4netBenchmark()
        {
            LogProvider.SetCurrentLogProvider(new Log4NetLogProvider());

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

            var repository = (Hierarchy)log4net.LogManager.GetRepository();
            var patternLayout = new PatternLayout { ConversionPattern = "%date [%thread] %-5level %logger {dd.env=%property{dd.env}, dd.service=%property{dd.service}, dd.version=%property{dd.version}, dd.trace_id=%property{dd.trace_id}, dd.span_id=%property{dd.span_id}} - %message%newline" };
            patternLayout.ActivateOptions();

#if DEBUG
            var writer = System.Console.Out;
#else
            var writer = TextWriter.Null;
#endif

            var appender = new TextWriterAppender { Layout = patternLayout, Writer = writer };

            repository.Root.AddAppender(appender);

            repository.Root.Level = Level.Info;
            repository.Configured = true;

            Logger = log4net.LogManager.GetLogger(typeof(Log4netBenchmark));
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
    }
}
