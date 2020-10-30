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
        private readonly ILogProvider _logProvider;
        private readonly ILog _logger;
        private readonly List<LogEvent> _logEvents;

        public SerilogLogProviderTests()
        {
            Serilog.Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Observers(obs => obs.Subscribe(logEvent => _logEvents.Add(logEvent)))
                .CreateLogger();
            _logEvents = new List<LogEvent>();

            _logProvider = new SerilogLogProvider();
            LogProvider.SetCurrentLogProvider(_logProvider);
            _logger = new LoggerExecutionWrapper(_logProvider.GetLogger("Test"));
        }

        [Fact]
        public void LogsInjectionEnabledAddsParentCorrelationIdentifiers()
        {
            // Assert that the Serilog log provider is correctly being used
            Assert.IsType<SerilogLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: true);
            LoggingProviderTestHelpers.LogInParentSpan(tracer, _logger, _logProvider.OpenMappedContext, out var parentScope, out var childScope);

            // Filter the logs
            _logEvents.RemoveAll(log => !log.MessageTemplate.ToString().Contains(LoggingProviderTestHelpers.LogPrefix));
            Assert.All(_logEvents, e => LogEventContains(e, tracer.DefaultServiceName, tracer.Settings.ServiceVersion, tracer.Settings.Environment, parentScope));
        }

        [Fact]
        public void LogsInjectionEnabledAddsChildCorrelationIdentifiers()
        {
            // Assert that the Serilog log provider is correctly being used
            Assert.IsType<SerilogLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: true);
            LoggingProviderTestHelpers.LogInChildSpan(tracer, _logger, _logProvider.OpenMappedContext, out var parentScope, out var childScope);

            // Filter the logs
            _logEvents.RemoveAll(log => !log.MessageTemplate.ToString().Contains(LoggingProviderTestHelpers.LogPrefix));
            Assert.All(_logEvents, e => LogEventContains(e, tracer.DefaultServiceName, tracer.Settings.ServiceVersion, tracer.Settings.Environment, childScope));
        }

        [Fact]
        public void LogsInjectionEnabledDoesNotAddCorrelationIdentifiersOutsideSpans()
        {
            // Assert that the Serilog log provider is correctly being used
            Assert.IsType<SerilogLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: true);
            LoggingProviderTestHelpers.LogOutsideSpans(tracer, _logger, _logProvider.OpenMappedContext, out var parentScope, out var childScope);

            // Filter the logs
            _logEvents.RemoveAll(log => !log.MessageTemplate.ToString().Contains(LoggingProviderTestHelpers.LogPrefix));
            Assert.All(_logEvents, e => LogEventDoesNotContainCorrelationIdentifiers(e));
        }

        [Fact]
        public void LogsInjectionEnabledUsesTracerServiceName()
        {
            // Assert that the Serilog log provider is correctly being used
            Assert.IsType<SerilogLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: true);
            LoggingProviderTestHelpers.LogInSpanWithServiceName(tracer, _logger, _logProvider.OpenMappedContext, "custom-service", out var scope);

            // Filter the logs
            _logEvents.RemoveAll(log => !log.MessageTemplate.ToString().Contains(LoggingProviderTestHelpers.LogPrefix));
            Assert.All(_logEvents, e => LogEventContains(e, tracer.DefaultServiceName, tracer.Settings.ServiceVersion, tracer.Settings.Environment, scope));
        }

        [Fact]
        public void DisabledLibLogSubscriberDoesNotAddCorrelationIdentifiers()
        {
            // Assert that the Serilog log provider is correctly being used
            Assert.IsType<SerilogLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: false);
            LoggingProviderTestHelpers.LogEverywhere(tracer, _logger, _logProvider.OpenMappedContext, out var parentScope, out var childScope);

            // Filter the logs
            _logEvents.RemoveAll(log => !log.MessageTemplate.ToString().Contains(LoggingProviderTestHelpers.LogPrefix));
            Assert.All(_logEvents, e => LogEventDoesNotContainCorrelationIdentifiers(e));
        }

        internal static void LogEventContains(Serilog.Events.LogEvent logEvent, string service, string version, string env, Scope scope)
        {
            Contains(logEvent, service, version, env, scope.Span.TraceId, scope.Span.SpanId);
        }

        internal static void Contains(Serilog.Events.LogEvent logEvent, string service, string version, string env, ulong traceId, ulong spanId)
        {
            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.SerilogServiceKey));
            Assert.Equal(service, logEvent.Properties[CorrelationIdentifier.SerilogServiceKey].ToString().Trim(new[] { '\"' }), ignoreCase: true);

            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.SerilogVersionKey));
            Assert.Equal(version, logEvent.Properties[CorrelationIdentifier.SerilogVersionKey].ToString().Trim(new[] { '\"' }), ignoreCase: true);

            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.SerilogEnvKey));
            Assert.Equal(env, logEvent.Properties[CorrelationIdentifier.SerilogEnvKey].ToString().Trim(new[] { '\"' }), ignoreCase: true);

            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.SerilogTraceIdKey));
            Assert.Equal(traceId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.SerilogTraceIdKey].ToString().Trim(new[] { '\"' })));

            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.SerilogSpanIdKey));
            Assert.Equal(spanId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.SerilogSpanIdKey].ToString().Trim(new[] { '\"' })));
        }

        internal static void LogEventDoesNotContainCorrelationIdentifiers(Serilog.Events.LogEvent logEvent)
        {
            // Do not assert on the version property
            // Do not assert on the service property
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.SerilogSpanIdKey));
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.SerilogTraceIdKey));
        }
    }
}
