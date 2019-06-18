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
            var tracer = InitializeTracer(enableLogsInjection: true);
            int logIndex = 0;
            string logString;

            // Start and make the parent scope active
            var parentScope = tracer.StartActive("parent");
            var parentSpan = parentScope.Span;

            // Emit a log event and verify the event is decorated with the parent scope properties
            _logger.Log(LogLevel.Info, () => "Started parent scope.");
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}={parentSpan.SpanId}", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}={parentSpan.TraceId}", logString);

            // Start and make the child scope active
            var childScope = tracer.StartActive("child");
            var childSpan = childScope.Span;

            // Emit a log event and verify the event is decorated with the child scope properties
            _logger.Log(LogLevel.Info, () => "Activated child scope.");
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}={childSpan.SpanId}", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}={childSpan.TraceId}", logString);

            // Close the child scope, making the parent scope active
            childScope.Close();

            // Emit a log event and verify the event is decorated with the parent span properties
            _logger.Log(LogLevel.Info, () => "Reactivated parent scope.");
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}={parentSpan.SpanId}", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}={parentSpan.TraceId}", logString);

            // Close the parent scope, so there is no active scope
            parentScope.Close();

            // Emit a log event and verify the event has zero values
            _logger.Log(LogLevel.Info, () => "No active scope.");
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
            var tracer = InitializeTracer(enableLogsInjection: true);
            int logIndex = 0;
            string logString;

            // Start and make the parent scope active
            var parentScope = tracer.StartActive("parent");
            var parentSpan = parentScope.Span;

            // Emit a log event and verify the event does not carry trace properties
            _logger.Log(LogLevel.Info, () => "Started parent scope.");
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=", logString);

            // Start and make the child scope active
            var childScope = tracer.StartActive("child");
            var childSpan = childScope.Span;

            // Emit a log event and verify the event does not carry trace properties
            _logger.Log(LogLevel.Info, () => "Activated child scope.");
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=", logString);

            // Close the child scope, making the parent scope active
            childScope.Close();

            // Emit a log event and verify the event does not carry trace properties
            _logger.Log(LogLevel.Info, () => "Reactivated parent scope.");
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=", logString);

            // Close the parent scope, so there is no active scope
            parentScope.Close();

            // Emit a log event and verify the event does not carry trace properties
            _logger.Log(LogLevel.Info, () => "No active scope.");
            logString = _target.Logs[logIndex++];
            Assert.Contains($"{CorrelationIdentifier.SpanIdKey}=", logString);
            Assert.Contains($"{CorrelationIdentifier.TraceIdKey}=", logString);
        }

        private Tracer InitializeTracer(bool enableLogsInjection)
        {
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ISampler>();

            settings.LogsInjectionEnabled = enableLogsInjection;

            return new Tracer(settings, writerMock.Object, samplerMock.Object, null);
        }
    }
}
