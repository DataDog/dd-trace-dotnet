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

            // Scope: Parent scope
            // Custom property: N/A
            logEvent = _logEvents[logIndex++];
            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.Equal<ulong>(parentScope.Span.SpanId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.SpanIdKey].ToString()));
            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
            Assert.Equal<ulong>(parentScope.Span.TraceId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.TraceIdKey].ToString()));
            Assert.False(logEvent.Properties.ContainsKey(LoggingProviderTestHelpers.CustomPropertyName));

            // Scope: Parent scope
            // Custom property: SET
            logEvent = _logEvents[logIndex++];
            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.Equal<ulong>(parentScope.Span.SpanId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.SpanIdKey].ToString()));
            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
            Assert.Equal<ulong>(parentScope.Span.TraceId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.TraceIdKey].ToString()));
            Assert.True(logEvent.Properties.ContainsKey(LoggingProviderTestHelpers.CustomPropertyName));
            Assert.Equal<int>(LoggingProviderTestHelpers.CustomPropertyValue, int.Parse(logEvent.Properties[LoggingProviderTestHelpers.CustomPropertyName].ToString()));

            // Scope: Child scope
            // Custom property: SET
            logEvent = _logEvents[logIndex++];
            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.Equal<ulong>(childScope.Span.SpanId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.SpanIdKey].ToString()));
            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
            Assert.Equal<ulong>(childScope.Span.TraceId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.TraceIdKey].ToString()));
            Assert.True(logEvent.Properties.ContainsKey(LoggingProviderTestHelpers.CustomPropertyName));
            Assert.Equal<int>(LoggingProviderTestHelpers.CustomPropertyValue, int.Parse(logEvent.Properties[LoggingProviderTestHelpers.CustomPropertyName].ToString()));

            // Scope: Parent scope
            // Custom property: SET
            logEvent = _logEvents[logIndex++];
            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.Equal<ulong>(parentScope.Span.SpanId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.SpanIdKey].ToString()));
            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
            Assert.Equal<ulong>(parentScope.Span.TraceId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.TraceIdKey].ToString()));
            Assert.True(logEvent.Properties.ContainsKey(LoggingProviderTestHelpers.CustomPropertyName));
            Assert.Equal<int>(LoggingProviderTestHelpers.CustomPropertyValue, int.Parse(logEvent.Properties[LoggingProviderTestHelpers.CustomPropertyName].ToString()));

            // Scope: Parent scope
            // Custom property: N/A
            logEvent = _logEvents[logIndex++];
            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.Equal<ulong>(parentScope.Span.SpanId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.SpanIdKey].ToString()));
            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
            Assert.Equal<ulong>(parentScope.Span.TraceId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.TraceIdKey].ToString()));
            Assert.False(logEvent.Properties.ContainsKey(LoggingProviderTestHelpers.CustomPropertyName));

            // Scope: N/A
            // Custom property: N/A
            logEvent = _logEvents[logIndex++];
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
            Assert.False(logEvent.Properties.ContainsKey(LoggingProviderTestHelpers.CustomPropertyName));
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

            // Scope: N/A
            // Custom property: N/A
            logEvent = _logEvents[logIndex++];
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
            Assert.False(logEvent.Properties.ContainsKey(LoggingProviderTestHelpers.CustomPropertyName));

            // Scope: N/A
            // Custom property: SET
            logEvent = _logEvents[logIndex++];
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
            Assert.True(logEvent.Properties.ContainsKey(LoggingProviderTestHelpers.CustomPropertyName));
            Assert.Equal<int>(LoggingProviderTestHelpers.CustomPropertyValue, int.Parse(logEvent.Properties[LoggingProviderTestHelpers.CustomPropertyName].ToString()));

            // Scope: N/A
            // Custom property: SET
            logEvent = _logEvents[logIndex++];
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
            Assert.True(logEvent.Properties.ContainsKey(LoggingProviderTestHelpers.CustomPropertyName));
            Assert.Equal<int>(LoggingProviderTestHelpers.CustomPropertyValue, int.Parse(logEvent.Properties[LoggingProviderTestHelpers.CustomPropertyName].ToString()));

            // Scope: N/A
            // Custom property: SET
            logEvent = _logEvents[logIndex++];
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
            Assert.True(logEvent.Properties.ContainsKey(LoggingProviderTestHelpers.CustomPropertyName));
            Assert.Equal<int>(LoggingProviderTestHelpers.CustomPropertyValue, int.Parse(logEvent.Properties[LoggingProviderTestHelpers.CustomPropertyName].ToString()));

            // Scope: N/A
            // Custom property: N/A
            logEvent = _logEvents[logIndex++];
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
            Assert.False(logEvent.Properties.ContainsKey(LoggingProviderTestHelpers.CustomPropertyName));

            // Scope: N/A
            // Custom property: N/A
            logEvent = _logEvents[logIndex++];
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.SpanIdKey));
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.TraceIdKey));
            Assert.False(logEvent.Properties.ContainsKey(LoggingProviderTestHelpers.CustomPropertyName));
        }
    }
}
