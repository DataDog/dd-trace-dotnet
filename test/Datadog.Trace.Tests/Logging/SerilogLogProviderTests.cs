using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.LogProviders;
using Datadog.Trace.Sampling;
using Moq;
using Serilog;
using Serilog.Events;
using Xunit;

namespace Datadog.Trace.Tests.Logging
{
    [Collection("Logging Test Collection")]
    public class SerilogLogProviderTests
    {
        private ILog _logger;
        private LogEvent _logEvent;

        public SerilogLogProviderTests()
        {
            Serilog.Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Observers(obs => obs.Subscribe(logEvent => _logEvent = logEvent))
                .CreateLogger();

            LogProvider.SetCurrentLogProvider(new SerilogLogProvider());
            _logger = LogProvider.GetLogger(typeof(SerilogLogProviderTests));
        }

        [Fact]
        public void EnabledLibLogSubscriberAddsTraceData()
        {
            // Assert that the Serilog log provider is correctly being used
            Assert.IsType<SerilogLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = InitializeTracer(enableLogsInjection: true);

            // Start and make the parent scope active
            var parentScope = tracer.StartActive("parent");
            var parentSpan = parentScope.Span;

            // Emit a log event and verify the event is decorated with the parent scope properties
            _logger.Log(LogLevel.Info, () => "Started parent scope.");
            Assert.True(_logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.Equal<ulong>(parentSpan.SpanId, ulong.Parse(_logEvent.Properties[CorrelationIdentifier.SpanIdKey].ToString()));
            Assert.True(_logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
            Assert.Equal<ulong>(parentSpan.TraceId, ulong.Parse(_logEvent.Properties[CorrelationIdentifier.TraceIdKey].ToString()));

            // Start and make the child scope active
            var childScope = tracer.StartActive("child");
            var childSpan = childScope.Span;

            // Emit a log event and verify the event is decorated with the child scope properties
            _logger.Log(LogLevel.Info, () => "Activated child scope.");
            Assert.True(_logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.Equal<ulong>(childSpan.SpanId, ulong.Parse(_logEvent.Properties[CorrelationIdentifier.SpanIdKey].ToString()));
            Assert.True(_logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
            Assert.Equal<ulong>(childSpan.TraceId, ulong.Parse(_logEvent.Properties[CorrelationIdentifier.TraceIdKey].ToString()));

            // Close the child scope, making the parent scope active
            childScope.Close();

            // Emit a log event and verify the event is decorated with the parent span properties
            _logger.Log(LogLevel.Info, () => "Reactivated parent scope.");
            Assert.True(_logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.Equal<ulong>(parentSpan.SpanId, ulong.Parse(_logEvent.Properties[CorrelationIdentifier.SpanIdKey].ToString()));
            Assert.True(_logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
            Assert.Equal<ulong>(parentSpan.TraceId, ulong.Parse(_logEvent.Properties[CorrelationIdentifier.TraceIdKey].ToString()));

            // Close the parent scope, so there is no active scope
            parentScope.Close();

            // Emit a log event and verify the event is not decorated with the properties
            _logger.Log(LogLevel.Info, () => "No active scope.");
            Assert.True(_logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.Equal<ulong>(0, ulong.Parse(_logEvent.Properties[CorrelationIdentifier.SpanIdKey].ToString()));
            Assert.True(_logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
            Assert.Equal<ulong>(0, ulong.Parse(_logEvent.Properties[CorrelationIdentifier.TraceIdKey].ToString()));
        }

        [Fact]
        public void DisabledLibLogSubscriberDoesNotAddTraceData()
        {
            // Assert that the Serilog log provider is correctly being used
            Assert.IsType<SerilogLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = InitializeTracer(enableLogsInjection: false);

            // Start and make the parent scope active
            var parentScope = tracer.StartActive("parent");
            var parentSpan = parentScope.Span;

            // Emit a log event and verify the event does not carry trace properties
            _logger.Log(LogLevel.Info, () => "Started parent scope.");
            Assert.False(_logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.False(_logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));

            // Start and make the child scope active
            var childScope = tracer.StartActive("child");
            var childSpan = childScope.Span;

            // Emit a log event and verify the event does not carry trace properties
            _logger.Log(LogLevel.Info, () => "Activated child scope.");
            Assert.False(_logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.False(_logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));

            // Close the child scope, making the parent scope active
            childScope.Close();

            // Emit a log event and verify the event does not carry trace properties
            _logger.Log(LogLevel.Info, () => "Reactivated parent scope.");
            Assert.False(_logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.False(_logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));

            // Close the parent scope, so there is no active scope
            parentScope.Close();

            // Emit a log event and verify the event does not carry trace properties
            _logger.Log(LogLevel.Info, () => "No active scope.");
            Assert.False(_logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.False(_logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
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
