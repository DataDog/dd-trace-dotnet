using System.IO;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Log4Net;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    [BenchmarkCategory(Constants.TracerCategory, Constants.RunOnPrs, Constants.RunOnMaster)]
    public class Log4netBenchmark
    {
        private log4net.ILog _logger;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var config = TracerHelper.DefaultConfig;
            config[ConfigurationKeys.LogsInjectionEnabled] = true;
            config[ConfigurationKeys.Environment] = "env";
            config[ConfigurationKeys.ServiceVersion] = "version";
            TracerHelper.SetGlobalTracer(config);

            var repository = (Hierarchy)log4net.LogManager.GetRepository();
            var patternLayout = new PatternLayout { ConversionPattern = "%date [%thread] %-5level %logger {dd.env=%property{dd.env}, dd.service=%property{dd.service}, dd.version=%property{dd.version}, dd.trace_id=%property{dd.trace_id}, dd.span_id=%property{dd.span_id}} - %message%newline" };
            patternLayout.ActivateOptions();

#if DEBUG
            var writer = System.Console.Out;
#else
            var writer = TextWriter.Null;
#endif
            var textWriterAppender = new TextWriterAppender { Layout = patternLayout, Writer = writer };
            var appender = new LogCorrelationAppender();
            appender.AddAppender(textWriterAppender);

            repository.Root.AddAppender(appender);

            repository.Root.Level = Level.Info;
            repository.Configured = true;

            _logger = log4net.LogManager.GetLogger(typeof(Log4netBenchmark));
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            TracerHelper.CleanupGlobalTracer();
        }

        [Benchmark]
        public void EnrichedLog()
        {
            using (Tracer.Instance.StartActive("Test"))
            {
                using (Tracer.Instance.StartActive("Child"))
                {
                    _logger.Info("Hello");
                }
            }
        }

        class LogCorrelationAppender : ForwardingAppender
        {
            protected override void Append(LoggingEvent loggingEvent)
            {
                var proxy = loggingEvent.DuckCast<ILoggingEvent>();
                // First argument isn't used, so can be anything
                AppenderAttachedImplIntegration.OnMethodBegin(loggingEvent, proxy);

                base.Append(loggingEvent);
            }
        }
    }
}
