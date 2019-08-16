using System.Collections.Generic;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.LogProviders;
using NLog;
using NLog.Config;
using NLog.Targets;
using Xunit;

namespace Datadog.Trace.Tests.Logging
{
    [Collection(nameof(Datadog.Trace.Tests.Logging))]
    public class NLogLogProviderTests
    {
        private readonly ILogProvider _logProvider;
        private readonly ILog _logger;
        private readonly MemoryTarget _target;

        public NLogLogProviderTests()
        {
            var config = new LoggingConfiguration();
            _target = new MemoryTarget
            {
                Layout = string.Format("${{level:uppercase=true}}|{0}=${{mdc:item={0}}}|{1}=${{mdc:item={1}}}|{2}=${{mdc:item={2}}}|${{message}}", CorrelationIdentifier.SpanIdKey, CorrelationIdentifier.TraceIdKey, LoggingProviderTestHelpers.CustomPropertyName)
            };

            config.AddTarget("memory", _target);
            config.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Trace, _target));
            LogManager.Configuration = config;
            SimpleConfigurator.ConfigureForTargetLogging(_target, NLog.LogLevel.Trace);

            _logProvider = new NLogLogProvider();
            LogProvider.SetCurrentLogProvider(_logProvider);
            _logger = new LoggerExecutionWrapper(_logProvider.GetLogger("test"));
        }

        [Fact]
        public void EnabledLibLogSubscriberAddsTraceData()
        {
            // Assert that the NLog log provider is correctly being used
            Assert.IsType<NLogLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: true);
            LoggingProviderTestHelpers.PerformParentChildScopeSequence(tracer, _logger, _logProvider.OpenMappedContext, out var parentScope, out var childScope);

            // Filter the logs
            List<string> filteredLogs = new List<string>(_target.Logs);
            filteredLogs.RemoveAll(log => !log.Contains(LoggingProviderTestHelpers.LogPrefix));

            int logIndex = 0;
            string logString;

            // Scope: Parent scope
            // Custom property: N/A
            logString = filteredLogs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}={parentScope.Span.SpanId}", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}={parentScope.Span.TraceId}", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}=", logString);

            // Scope: Parent scope
            // Custom property: SET
            logString = filteredLogs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}={parentScope.Span.SpanId}", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}={parentScope.Span.TraceId}", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}={LoggingProviderTestHelpers.CustomPropertyValue}", logString);

            // Scope: Child scope
            // Custom property: SET
            logString = filteredLogs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}={childScope.Span.SpanId}", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}={childScope.Span.TraceId}", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}={LoggingProviderTestHelpers.CustomPropertyValue}", logString);

            // Scope: Parent scope
            // Custom property: SET
            logString = filteredLogs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}={parentScope.Span.SpanId}", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}={parentScope.Span.TraceId}", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}={LoggingProviderTestHelpers.CustomPropertyValue}", logString);

            // Scope: Parent scope
            // Custom property: N/A
            logString = filteredLogs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}={parentScope.Span.SpanId}", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}={parentScope.Span.TraceId}", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}=", logString);

            // Scope: Default values of TraceId=0,SpanId=0
            // Custom property: N/A
            logString = filteredLogs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=0", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=0", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}=", logString);
        }

        [Fact]
        public void DisabledLibLogSubscriberDoesNotAddTraceData()
        {
            // Assert that the NLog log provider is correctly being used
            Assert.IsType<NLogLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: false);
            LoggingProviderTestHelpers.PerformParentChildScopeSequence(tracer, _logger, _logProvider.OpenMappedContext, out var parentScope, out var childScope);

            // Filter the logs
            List<string> filteredLogs = new List<string>(_target.Logs);
            filteredLogs.RemoveAll(log => !log.Contains(LoggingProviderTestHelpers.LogPrefix));

            int logIndex = 0;
            string logString;

            // Scope: N/A
            // Custom property: N/A
            logString = filteredLogs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}=", logString);

            // Scope: N/A
            // Custom property: SET
            logString = filteredLogs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}={LoggingProviderTestHelpers.CustomPropertyValue}", logString);

            // Scope: N/A
            // Custom property: SET
            logString = filteredLogs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}={LoggingProviderTestHelpers.CustomPropertyValue}", logString);

            // Scope: N/A
            // Custom property: SET
            logString = filteredLogs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}={LoggingProviderTestHelpers.CustomPropertyValue}", logString);

            // Scope: N/A
            // Custom property: N/A
            logString = filteredLogs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}=", logString);

            // Scope: N/A
            // Custom property: N/A
            logString = filteredLogs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}=", logString);
        }
    }
}
