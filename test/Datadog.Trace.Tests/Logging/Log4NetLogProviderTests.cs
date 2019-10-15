using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.LogProviders;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;
using Newtonsoft.Json;
using Xunit;

namespace Datadog.Trace.Tests.Logging
{
    [Collection(nameof(Datadog.Trace.Tests.Logging))]
    public class Log4NetLogProviderTests
    {
        private readonly ILogProvider _logProvider;
        private readonly ILog _logger;
        private readonly MemoryAppender _memoryAppender;

        public Log4NetLogProviderTests()
        {
            _memoryAppender = new MemoryAppender();
            var repository = log4net.LogManager.GetRepository(Assembly.GetAssembly(typeof(log4net.LogManager)));
            BasicConfigurator.Configure(repository, _memoryAppender);

            _logProvider = new Log4NetLogProvider();
            LogProvider.SetCurrentLogProvider(_logProvider);
            _logger = new LoggerExecutionWrapper(_logProvider.GetLogger("Test"));
        }

        [Fact]
        public void EnabledLibLogSubscriberAddsTraceData()
        {
            // Assert that the Log4Net log provider is correctly being used
            Assert.IsType<Log4NetLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: true);
            LoggingProviderTestHelpers.PerformParentChildScopeSequence(tracer, _logger, _logProvider.OpenMappedContext, out var parentScope, out var childScope);

            // Filter the logs
            List<LoggingEvent> filteredLogs = new List<LoggingEvent>(_memoryAppender.GetEvents());
            filteredLogs.RemoveAll(log => !log.MessageObject.ToString().Contains(LoggingProviderTestHelpers.LogPrefix));

            int logIndex = 0;
            LoggingEvent logEvent;

            // Scope: Parent scope
            // Custom property: N/A
            logEvent = filteredLogs[logIndex++];
            logEvent.Contains(parentScope);
            Assert.DoesNotContain(LoggingProviderTestHelpers.CustomPropertyName, logEvent.Properties.GetKeys());

            // Scope: Parent scope
            // Custom property: SET
            logEvent = filteredLogs[logIndex++];
            logEvent.Contains(parentScope);
            Assert.Contains(LoggingProviderTestHelpers.CustomPropertyName, logEvent.Properties.GetKeys());
            Assert.Equal<int>(LoggingProviderTestHelpers.CustomPropertyValue, int.Parse(logEvent.Properties[LoggingProviderTestHelpers.CustomPropertyName].ToString()));

            // Scope: Child scope
            // Custom property: SET
            logEvent = filteredLogs[logIndex++];
            logEvent.Contains(childScope);
            Assert.Contains(LoggingProviderTestHelpers.CustomPropertyName, logEvent.Properties.GetKeys());
            Assert.Equal<int>(LoggingProviderTestHelpers.CustomPropertyValue, int.Parse(logEvent.Properties[LoggingProviderTestHelpers.CustomPropertyName].ToString()));

            // Scope: Parent scope
            // Custom property: SET
            logEvent = filteredLogs[logIndex++];
            logEvent.Contains(parentScope);
            Assert.Contains(LoggingProviderTestHelpers.CustomPropertyName, logEvent.Properties.GetKeys());
            Assert.Equal<int>(LoggingProviderTestHelpers.CustomPropertyValue, int.Parse(logEvent.Properties[LoggingProviderTestHelpers.CustomPropertyName].ToString()));

            // EXISTING: Verify the log event is decorated with the parent scope properties
            // Scope: Parent scope
            // Custom property: N/A
            logEvent = filteredLogs[logIndex++];
            logEvent.Contains(parentScope);
            Assert.DoesNotContain(LoggingProviderTestHelpers.CustomPropertyName, logEvent.Properties.GetKeys());

            // Scope: Default values of TraceId=0,SpanId=0
            // Custom property: N/A
            logEvent = filteredLogs[logIndex++];
            logEvent.Contains(traceId: 0, spanId: 0);
            Assert.DoesNotContain(LoggingProviderTestHelpers.CustomPropertyName, logEvent.Properties.GetKeys());
        }

        [Fact]
        public void DisabledLibLogSubscriberDoesNotAddTraceData()
        {
            // Assert that the Log4Net log provider is correctly being used
            Assert.IsType<Log4NetLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: false);
            LoggingProviderTestHelpers.PerformParentChildScopeSequence(tracer, _logger, _logProvider.OpenMappedContext, out var parentScope, out var childScope);

            // Filter the logs
            List<LoggingEvent> filteredLogs = new List<LoggingEvent>(_memoryAppender.GetEvents());
            filteredLogs.RemoveAll(log => !log.MessageObject.ToString().Contains(LoggingProviderTestHelpers.LogPrefix));

            int logIndex = 0;
            LoggingEvent logEvent;

            // Scope: N/A
            // Custom property: N/A
            logEvent = filteredLogs[logIndex++];
            Assert.DoesNotContain(CorrelationIdentifier.SpanIdKey, logEvent.Properties.GetKeys());
            Assert.DoesNotContain(CorrelationIdentifier.TraceIdKey, logEvent.Properties.GetKeys());
            Assert.DoesNotContain(LoggingProviderTestHelpers.CustomPropertyName, logEvent.Properties.GetKeys());

            // Scope: N/A
            // Custom property: SET
            logEvent = filteredLogs[logIndex++];
            Assert.DoesNotContain(CorrelationIdentifier.SpanIdKey, logEvent.Properties.GetKeys());
            Assert.DoesNotContain(CorrelationIdentifier.TraceIdKey, logEvent.Properties.GetKeys());
            Assert.Contains(LoggingProviderTestHelpers.CustomPropertyName, logEvent.Properties.GetKeys());
            Assert.Equal<int>(LoggingProviderTestHelpers.CustomPropertyValue, int.Parse(logEvent.Properties[LoggingProviderTestHelpers.CustomPropertyName].ToString()));

            // Scope: N/A
            // Custom property: SET
            logEvent = filteredLogs[logIndex++];
            Assert.DoesNotContain(CorrelationIdentifier.SpanIdKey, logEvent.Properties.GetKeys());
            Assert.DoesNotContain(CorrelationIdentifier.TraceIdKey, logEvent.Properties.GetKeys());
            Assert.Contains(LoggingProviderTestHelpers.CustomPropertyName, logEvent.Properties.GetKeys());
            Assert.Equal<int>(LoggingProviderTestHelpers.CustomPropertyValue, int.Parse(logEvent.Properties[LoggingProviderTestHelpers.CustomPropertyName].ToString()));

            // Scope: N/A
            // Custom property: SET
            logEvent = filteredLogs[logIndex++];
            Assert.DoesNotContain(CorrelationIdentifier.SpanIdKey, logEvent.Properties.GetKeys());
            Assert.DoesNotContain(CorrelationIdentifier.TraceIdKey, logEvent.Properties.GetKeys());
            Assert.Contains(LoggingProviderTestHelpers.CustomPropertyName, logEvent.Properties.GetKeys());
            Assert.Equal<int>(LoggingProviderTestHelpers.CustomPropertyValue, int.Parse(logEvent.Properties[LoggingProviderTestHelpers.CustomPropertyName].ToString()));

            // Scope: N/A
            // Custom property: N/A
            logEvent = filteredLogs[logIndex++];
            Assert.DoesNotContain(CorrelationIdentifier.SpanIdKey, logEvent.Properties.GetKeys());
            Assert.DoesNotContain(CorrelationIdentifier.TraceIdKey, logEvent.Properties.GetKeys());
            Assert.DoesNotContain(LoggingProviderTestHelpers.CustomPropertyName, logEvent.Properties.GetKeys());

            // Scope: N/A
            // Custom property: N/A
            logEvent = filteredLogs[logIndex++];
            Assert.DoesNotContain(CorrelationIdentifier.SpanIdKey, logEvent.Properties.GetKeys());
            Assert.DoesNotContain(CorrelationIdentifier.TraceIdKey, logEvent.Properties.GetKeys());
            Assert.DoesNotContain(LoggingProviderTestHelpers.CustomPropertyName, logEvent.Properties.GetKeys());
        }

        /// <summary>
        /// Lighweight JSON-formatter for Log4Net inspired by https://github.com/Litee/log4net.Layout.Json
        /// </summary>
        internal class Log4NetJsonLayout : LayoutSkeleton
        {
            public override void ActivateOptions()
            {
            }

            public override void Format(TextWriter writer, LoggingEvent e)
            {
                var dic = new Dictionary<string, object>
                {
                    ["level"] = e.Level.DisplayName,
                    ["messageObject"] = e.MessageObject,
                    ["renderedMessage"] = e.RenderedMessage,
                    ["timestampUtc"] = e.TimeStamp.ToUniversalTime().ToString("O"),
                    ["logger"] = e.LoggerName,
                    ["thread"] = e.ThreadName,
                    ["exceptionObject"] = e.ExceptionObject,
                    ["exceptionObjectString"] = e.ExceptionObject == null ? null : e.GetExceptionString(),
                    ["userName"] = e.UserName,
                    ["domain"] = e.Domain,
                    ["identity"] = e.Identity,
                    ["location"] = e.LocationInformation.FullInfo,
                    ["properties"] = e.GetProperties()
                };
                writer.Write(JsonConvert.SerializeObject(dic));
            }
        }
    }
}
