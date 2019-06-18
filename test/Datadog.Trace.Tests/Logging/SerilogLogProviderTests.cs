using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.LogProviders;
using Serilog;
using Serilog.Events;
using Xunit;

namespace Datadog.Trace.Tests.Logging
{
    [Collection(nameof(Datadog.Trace.Tests.Logging))]
    public class SerilogLogProviderTests
    {
        private readonly ILog _logger;
        private readonly List<LogEvent> _logEvents;

        public SerilogLogProviderTests()
        {
            Serilog.Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Observers(obs => obs.Subscribe(logEvent => _logEvents.Add(logEvent)))
                .CreateLogger();

            LogProvider.SetCurrentLogProvider(new SerilogLogProvider());
            _logger = LogProvider.GetLogger(typeof(SerilogLogProviderTests));
            _logEvents = new List<LogEvent>();
        }

        [Fact]
        public void EnabledLibLogSubscriberAddsTraceData()
        {
            // Assert that the Serilog log provider is correctly being used
            Assert.IsType<SerilogLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: true);
            LoggingProviderTestHelpers.PerformParentChildScopeSequence(tracer, _logger, out var parentScope, out var childScope);

            var logIndex = 0;
            LogEvent logEvent;

            // Verify the log event is decorated with the parent scope properties
            logEvent = _logEvents[logIndex++];
            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.Equal<ulong>(parentScope.Span.SpanId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.SpanIdKey].ToString()));
            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
            Assert.Equal<ulong>(parentScope.Span.TraceId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.TraceIdKey].ToString()));

            // Verify the log event is decorated with the child scope properties
            logEvent = _logEvents[logIndex++];
            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.Equal<ulong>(childScope.Span.SpanId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.SpanIdKey].ToString()));
            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
            Assert.Equal<ulong>(childScope.Span.TraceId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.TraceIdKey].ToString()));

            // Verify the log event is decorated with the parent scope properties
            logEvent = _logEvents[logIndex++];
            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.Equal<ulong>(parentScope.Span.SpanId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.SpanIdKey].ToString()));
            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
            Assert.Equal<ulong>(parentScope.Span.TraceId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.TraceIdKey].ToString()));

            // Verify the log event is decorated with zero values
            logEvent = _logEvents[logIndex++];
            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.Equal<ulong>(0, ulong.Parse(logEvent.Properties[CorrelationIdentifier.SpanIdKey].ToString()));
            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
            Assert.Equal<ulong>(0, ulong.Parse(logEvent.Properties[CorrelationIdentifier.TraceIdKey].ToString()));
        }

        [Fact]
        public void DisabledLibLogSubscriberDoesNotAddTraceData()
        {
            // Assert that the Serilog log provider is correctly being used
            Assert.IsType<SerilogLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: false);
            LoggingProviderTestHelpers.PerformParentChildScopeSequence(tracer, _logger, out var parentScope, out var childScope);

            int logIndex = 0;
            LogEvent logEvent;

            // Verify the log event is not decorated with the properties
            logEvent = _logEvents[logIndex++];
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));

            // Verify the log event is not decorated with the properties
            logEvent = _logEvents[logIndex++];
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));

            // Verify the log event is not decorated with the properties
            logEvent = _logEvents[logIndex++];
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));

            // Verify the log event is not decorated with the properties
            logEvent = _logEvents[logIndex++];
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
        }
    }
}
