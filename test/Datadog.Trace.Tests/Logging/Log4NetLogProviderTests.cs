using System.Reflection;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.LogProviders;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using Xunit;

namespace Datadog.Trace.Tests.Logging
{
    [Collection(nameof(Datadog.Trace.Tests.Logging))]
    public class Log4NetLogProviderTests
    {
        private readonly MemoryAppender _memoryAppender;
        private ILog _logger;
        // private LogEvent _logEvent;

        public Log4NetLogProviderTests()
        {
            _memoryAppender = new MemoryAppender();
            var repository = log4net.LogManager.GetRepository(Assembly.GetAssembly(typeof(log4net.LogManager)));
            BasicConfigurator.Configure(repository, _memoryAppender);

            LogProvider.SetCurrentLogProvider(new Log4NetLogProvider());
            _logger = LogProvider.GetLogger(typeof(Log4NetLogProviderTests));
        }

        [Fact]
        public void EnabledLibLogSubscriberAddsTraceData()
        {
            // Assert that the Log4Net log provider is correctly being used
            Assert.IsType<Log4NetLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: true);
            LoggingProviderTestHelpers.PerformParentChildScopeSequence(tracer, _logger, out var parentScope, out var childScope);

            int logIndex = 0;
            var logEvents = _memoryAppender.GetEvents();
            LoggingEvent logEvent;

            // Verify the log event is decorated with the parent scope properties
            logEvent = logEvents[logIndex++];
            Assert.Contains(CorrelationIdentifier.SpanIdKey, logEvent.Properties.GetKeys());
            Assert.Equal<ulong>(parentScope.Span.SpanId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.SpanIdKey].ToString()));
            Assert.Contains(CorrelationIdentifier.TraceIdKey, logEvent.Properties.GetKeys());
            Assert.Equal<ulong>(parentScope.Span.TraceId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.TraceIdKey].ToString()));

            // Verify the log event is decorated with the child scope properties
            logEvent = logEvents[logIndex++];
            Assert.Contains(CorrelationIdentifier.SpanIdKey, logEvent.Properties.GetKeys());
            Assert.Equal<ulong>(childScope.Span.SpanId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.SpanIdKey].ToString()));
            Assert.Contains(CorrelationIdentifier.TraceIdKey, logEvent.Properties.GetKeys());
            Assert.Equal<ulong>(childScope.Span.TraceId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.TraceIdKey].ToString()));

            // Verify the log event is decorated with the parent scope properties
            logEvent = logEvents[logIndex++];
            Assert.Contains(CorrelationIdentifier.SpanIdKey, logEvent.Properties.GetKeys());
            Assert.Equal<ulong>(parentScope.Span.SpanId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.SpanIdKey].ToString()));
            Assert.Contains(CorrelationIdentifier.TraceIdKey, logEvent.Properties.GetKeys());
            Assert.Equal<ulong>(parentScope.Span.TraceId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.TraceIdKey].ToString()));

            // Verify the log event is decorated with zero values
            logEvent = logEvents[logIndex++];
            Assert.Contains(CorrelationIdentifier.SpanIdKey, logEvent.Properties.GetKeys());
            Assert.Equal<ulong>(0, ulong.Parse(logEvent.Properties[CorrelationIdentifier.SpanIdKey].ToString()));
            Assert.Contains(CorrelationIdentifier.TraceIdKey, logEvent.Properties.GetKeys());
            Assert.Equal<ulong>(0, ulong.Parse(logEvent.Properties[CorrelationIdentifier.TraceIdKey].ToString()));
        }

        [Fact]
        public void DisabledLibLogSubscriberDoesNotAddTraceData()
        {
            // Assert that the Log4Net log provider is correctly being used
            Assert.IsType<Log4NetLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: false);
            LoggingProviderTestHelpers.PerformParentChildScopeSequence(tracer, _logger, out var parentScope, out var childScope);

            int logIndex = 0;
            var logEvents = _memoryAppender.GetEvents();
            LoggingEvent logEvent;

            // Verify the log event is not decorated with the properties
            logEvent = logEvents[logIndex++];
            Assert.DoesNotContain(CorrelationIdentifier.SpanIdKey, logEvent.Properties.GetKeys());
            Assert.DoesNotContain(CorrelationIdentifier.TraceIdKey, logEvent.Properties.GetKeys());

            // Verify the log event is not decorated with the properties
            logEvent = logEvents[logIndex++];
            Assert.DoesNotContain(CorrelationIdentifier.SpanIdKey, logEvent.Properties.GetKeys());
            Assert.DoesNotContain(CorrelationIdentifier.TraceIdKey, logEvent.Properties.GetKeys());

            // Verify the log event is not decorated with the properties
            logEvent = logEvents[logIndex++];
            Assert.DoesNotContain(CorrelationIdentifier.SpanIdKey, logEvent.Properties.GetKeys());
            Assert.DoesNotContain(CorrelationIdentifier.TraceIdKey, logEvent.Properties.GetKeys());

            // Verify the log event is not decorated with the properties
            logEvent = logEvents[logIndex++];
            Assert.DoesNotContain(CorrelationIdentifier.SpanIdKey, logEvent.Properties.GetKeys());
            Assert.DoesNotContain(CorrelationIdentifier.TraceIdKey, logEvent.Properties.GetKeys());
        }
    }
}
