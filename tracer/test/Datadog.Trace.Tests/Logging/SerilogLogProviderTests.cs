// <copyright file="SerilogLogProviderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
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

            _logProvider = new CustomSerilogLogProvider();
            LogProvider.SetCurrentLogProvider(_logProvider);
            _logger = new LoggerExecutionWrapper(_logProvider.GetLogger("Test"));
        }

        [Fact]
        public void LogsInjectionEnabledAddsParentCorrelationIdentifiers()
        {
            // Assert that the Serilog log provider is correctly being used
            Assert.IsType<CustomSerilogLogProvider>(LogProvider.CurrentLogProvider);

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
            Assert.IsType<CustomSerilogLogProvider>(LogProvider.CurrentLogProvider);

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
            Assert.IsType<CustomSerilogLogProvider>(LogProvider.CurrentLogProvider);

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
            Assert.IsType<CustomSerilogLogProvider>(LogProvider.CurrentLogProvider);

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
            Assert.IsType<CustomSerilogLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to FALSE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: false);
            LoggingProviderTestHelpers.LogEverywhere(tracer, _logger, _logProvider.OpenMappedContext, out var parentScope, out var childScope);

            // Filter the logs
            _logEvents.RemoveAll(log => !log.MessageTemplate.ToString().Contains(LoggingProviderTestHelpers.LogPrefix));
            Assert.All(_logEvents, e => LogEventDoesNotContainCorrelationIdentifiers(e));
        }

        [Fact]
        public void LogTheRightSpan()
        {
            // Assert that the Serilog log provider is correctly being used
            Assert.IsType<CustomSerilogLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: true);

            var barrier1 = new Barrier(2);
            var barrier2 = new Barrier(2);

            var task1 = Task.Run(() => Thread1(barrier1, tracer));
            barrier1.SignalAndWait(); // Wait until thread1 has created both scopes

            var task2 = Task.Run(() => Thread2(barrier2, tracer));
            barrier2.SignalAndWait(); // Wait until thread2 has created both scopes

            // Both threads have created a span

            barrier1.SignalAndWait(); // Release thread1
            barrier1.SignalAndWait(); // Wait until thread1 has disposed the inner scope

            // Thread1 has disposed the inner span, outer span is still open

            barrier2.SignalAndWait(); // Release thread2
            // Thread2 closes the inner span. If the interleaved operations had no wrong effect,
            // the first log should have a span and the second log shouldn't
            task2.Wait();

            // Unblock the first thread
            barrier1.SignalAndWait();
            task1.Wait();

            Assert.Equal(2, _logEvents.Count);

            var spanLog = _logEvents[0];

            Assert.StartsWith("Span", spanLog.MessageTemplate.Text);
            Assert.Equal(5, spanLog.Properties.Count);

            var expectedSpanId = spanLog.MessageTemplate.Text.Split('-')[1];
            var spanProperty = spanLog.Properties[CorrelationIdentifier.SerilogSpanIdKey];
            Assert.Equal(expectedSpanId, spanProperty.ToString().Trim('\"'));

            var noSpanLog = _logEvents[1];
            Assert.StartsWith("NoSpan", noSpanLog.MessageTemplate.ToString());
            Assert.Empty(noSpanLog.Properties);
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

        private static void Thread1(Barrier barrier, Tracer tracer)
        {
            using (tracer.StartActive("Outer"))
            {
                using (tracer.StartActive("Inner"))
                {
                    barrier.SignalAndWait();
                    barrier.SignalAndWait();
                }

                barrier.SignalAndWait();
                barrier.SignalAndWait();
            }
        }

        private static void Thread2(Barrier barrier, Tracer tracer)
        {
            using (var outerScope = tracer.StartActive("Outer"))
            {
                using (tracer.StartActive("Inner"))
                {
                    barrier.SignalAndWait();
                    barrier.SignalAndWait();
                }

                Log.Information("Span-" + outerScope.Span.SpanId);
            }

            Log.Information("NoSpan");
        }
    }
}
