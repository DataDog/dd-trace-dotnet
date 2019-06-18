using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.LogProviders;
using Datadog.Trace.Sampling;
using Moq;
using NLog;
using NLog.Config;
using NLog.Targets;
using Xunit;
using LogLevel = Datadog.Trace.Logging.LogLevel;

namespace Datadog.Trace.Tests.Logging
{
    [Collection("Logging Test Collection")]
    public class NLogLogProviderTests
    {
        private readonly ILog _logger;
        private readonly MemoryTarget _target;

        public NLogLogProviderTests()
        {
            var config = new LoggingConfiguration();
            _target = new MemoryTarget
            {
                Layout = string.Format("${{level:uppercase=true}}|{0}=${{mdc:item={0}}}|{1}=${{mdc:item={1}}}|${{message}}", CorrelationIdentifier.SpanIdKey, CorrelationIdentifier.TraceIdKey)
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

            // Verify the log event is decorated with the parent scope properties
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}={parentScope.Span.SpanId}", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}={parentScope.Span.TraceId}", logString);

            // Verify the log event is decorated with the child scope properties
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}={childScope.Span.SpanId}", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}={childScope.Span.TraceId}", logString);

            // Verify the log event is decorated with the parent scope properties
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}={parentScope.Span.SpanId}", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}={parentScope.Span.TraceId}", logString);

            // Verify the log event is decorated with zero values
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=0", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=0", logString);
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

            // Verify the log event is not decorated with the properties
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=", logString);

            // Verify the log event is not decorated with the properties
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=", logString);

            // Verify the log event is not decorated with the properties
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=", logString);

            // Verify the log event is not decorated with the properties
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=", logString);
        }
    }
}
