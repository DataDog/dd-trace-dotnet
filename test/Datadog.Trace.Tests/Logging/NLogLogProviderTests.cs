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

            LogProvider.SetCurrentLogProvider(new NLogLogProvider());
            _logger = LogProvider.GetLogger(typeof(NLogLogProviderTests));
        }

        [Fact]
        public void EnabledLibLogSubscriberAddsTraceData()
        {
            // Assert that the NLog log provider is correctly being used
            Assert.IsType<NLogLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: true);
            LoggingProviderTestHelpers.PerformParentChildScopeSequence(tracer, _logger, out var parentScope, out var childScope);

            int logIndex = 0;
            string logString;

            // Scope: Parent scope
            // Custom property: N/A
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}={parentScope.Span.SpanId}", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}={parentScope.Span.TraceId}", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}=", logString);

            // Scope: Parent scope
            // Custom property: SET
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}={parentScope.Span.SpanId}", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}={parentScope.Span.TraceId}", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}={LoggingProviderTestHelpers.CustomPropertyValue}", logString);

            // Scope: Child scope
            // Custom property: SET
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}={childScope.Span.SpanId}", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}={childScope.Span.TraceId}", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}={LoggingProviderTestHelpers.CustomPropertyValue}", logString);

            // Scope: Parent scope
            // Custom property: SET
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}={parentScope.Span.SpanId}", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}={parentScope.Span.TraceId}", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}={LoggingProviderTestHelpers.CustomPropertyValue}", logString);

            // Scope: Parent scope
            // Custom property: N/A
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}={parentScope.Span.SpanId}", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}={parentScope.Span.TraceId}", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}=", logString);

            // Scope: N/A
            // Custom property: N/A
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}=", logString);
        }

        [Fact]
        public void DisabledLibLogSubscriberDoesNotAddTraceData()
        {
            // Assert that the NLog log provider is correctly being used
            Assert.IsType<NLogLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: false);
            LoggingProviderTestHelpers.PerformParentChildScopeSequence(tracer, _logger, out var parentScope, out var childScope);

            int logIndex = 0;
            string logString;

            // Scope: N/A
            // Custom property: N/A
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}=", logString);

            // Scope: N/A
            // Custom property: SET
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}={LoggingProviderTestHelpers.CustomPropertyValue}", logString);

            // Scope: N/A
            // Custom property: SET
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}={LoggingProviderTestHelpers.CustomPropertyValue}", logString);

            // Scope: N/A
            // Custom property: SET
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}={LoggingProviderTestHelpers.CustomPropertyValue}", logString);

            // Scope: N/A
            // Custom property: N/A
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}=", logString);

            // Scope: N/A
            // Custom property: N/A
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=", logString);
            Assert.Contains($"{LoggingProviderTestHelpers.CustomPropertyName}=", logString);
        }
    }
}
