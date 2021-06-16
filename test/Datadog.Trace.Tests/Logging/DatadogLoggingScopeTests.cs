// <copyright file="DatadogLoggingScopeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP
using System;
using System.Collections.Generic;
using Datadog.Trace.AspNetCore;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Datadog.Trace.Tests.Logging
{
    [Collection(nameof(Datadog.Trace.Tests.Logging))]
    public class DatadogLoggingScopeTests : IDisposable
    {
        private static readonly string LogPrefix = LoggingProviderTestHelpers.LogPrefix;
        private static readonly Dictionary<string, int> CustomScope = new() { { LoggingProviderTestHelpers.CustomPropertyName, LoggingProviderTestHelpers.CustomPropertyValue } };
        private readonly List<LogEvent> _logEvents;
        private readonly LoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly Tracer _tracer;

        public DatadogLoggingScopeTests()
        {
            _logEvents = new List<LogEvent>();

            var logProvider = new InternalScopeLoggerProvider(_logEvents);
            _loggerFactory = new LoggerFactory(new[] { logProvider });
            _logger = _loggerFactory.CreateLogger("Test");

            // Instantiate a _tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            // Currently, the DatadogLoggingScope does not require enableLogsInjection - it is the automatic
            // instrumentation that will add the scope as required instead
            _tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: true);
        }

        public void Dispose() => _loggerFactory?.Dispose();

        [Fact]
        public void LogsInjectionEnabledAddsParentCorrelationIdentifiers()
        {
            Scope parentScope;
            using (parentScope = _tracer.StartActive("parent"))
            {
                using (_logger.BeginScope(CustomScope))
                using (_logger.BeginScope(new DatadogLoggingScope(_tracer)))
                {
                    _logger.LogInformation($"{LogPrefix}Started and activated parent scope.");

                    using (var childScope = _tracer.StartActive("child"))
                    {
                        // Empty
                    }

                    _logger.LogInformation($"{LogPrefix}Closed child scope and reactivated parent scope.");
                }
            }

            // Filter the logs
            _logEvents.RemoveAll(log => !log.Message.Contains(LogPrefix));
            _logEvents.Should().NotBeEmpty();
            Assert.All(_logEvents, e => LogEventContains(e, _tracer.DefaultServiceName, _tracer.Settings.ServiceVersion, _tracer.Settings.Environment, parentScope));
        }

        [Fact]
        public void LogsInjectionEnabledAddsChildCorrelationIdentifiers()
        {
            Scope childScope;
            using (var parentScope = _tracer.StartActive("parent"))
            {
                using (_logger.BeginScope(CustomScope))
                using (_logger.BeginScope(new DatadogLoggingScope(_tracer)))
                {
                    using (childScope = _tracer.StartActive("child"))
                    {
                        _logger.LogInformation($"{LogPrefix}Started and activated child scope.");
                    }
                }
            }

            // Filter the logs
            _logEvents.RemoveAll(log => !log.Message.ToString().Contains(LogPrefix));
            Assert.All(_logEvents, e => LogEventContains(e, _tracer.DefaultServiceName, _tracer.Settings.ServiceVersion, _tracer.Settings.Environment, childScope));
        }

        [Fact]
        public void LogsInjectionEnabledDoesNotAddCorrelationIdentifiersOutsideSpans()
        {
            _logger.LogInformation($"{LogPrefix}Logged before starting/activating a scope");

            using (var parentScope = _tracer.StartActive("parent"))
            {
                using (_logger.BeginScope(CustomScope))
                using (_logger.BeginScope(new DatadogLoggingScope(_tracer)))
                {
                    using (var childScope = _tracer.StartActive("child"))
                    {
                        // Empty
                    }
                }
            }

            _logger.LogInformation($"{LogPrefix}Closed child scope so there is no active scope.");

            // Filter the logs
            _logEvents.RemoveAll(log => !log.Message.ToString().Contains(LogPrefix));
            Assert.All(_logEvents, e => LogEventDoesNotContainCorrelationIdentifiers(e));
        }

        [Fact]
        public void LogsInjectionEnabledUsesTracerServiceName()
        {
            Scope scope;
            using (scope = _tracer.StartActive("span", serviceName: "custom-service"))
            {
                using (_logger.BeginScope(CustomScope))
                using (_logger.BeginScope(new DatadogLoggingScope(_tracer)))
                {
                    _logger.LogInformation($"{LogPrefix}Entered single scope with a different service name.");
                }
            }

            // Filter the logs
            _logEvents.RemoveAll(log => !log.Message.ToString().Contains(LogPrefix));
            Assert.All(_logEvents, e => LogEventContains(e, _tracer.DefaultServiceName, _tracer.Settings.ServiceVersion, _tracer.Settings.Environment, scope));
        }

        internal static void LogEventContains(LogEvent logEvent, string service, string version, string env, Scope scope)
        {
            Contains(logEvent, service, version, env, scope.Span.TraceId, scope.Span.SpanId);
        }

        internal static void Contains(LogEvent logEvent, string service, string version, string env, ulong traceId, ulong spanId)
        {
            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.SerilogServiceKey));
            Assert.Equal(service, logEvent.Properties[CorrelationIdentifier.SerilogServiceKey].ToString().Trim(new[] { '\"' }), ignoreCase: true);

            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.SerilogVersionKey));
            Assert.Equal(version, logEvent.Properties[CorrelationIdentifier.SerilogVersionKey].ToString().Trim(new[] { '\"' }), ignoreCase: true);

            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.SerilogEnvKey));
            Assert.Equal(env, logEvent.Properties[CorrelationIdentifier.SerilogEnvKey].ToString().Trim(new[] { '\"' }), ignoreCase: true);

            Assert.True(logEvent.Properties.ContainsKey(CorrelationIdentifier.SerilogTraceIdKey));
            Assert.Equal(traceId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.SerilogTraceIdKey].ToString().Trim(new[] { '\"' })));

            // NOTE: we can't safely inject the span ID
            // The ILogger instrumentation requires a TraceId is already present when the scope is opened
            // And it isn't updated again, so can't capture span transitions correctly
        }

        internal static void LogEventDoesNotContainCorrelationIdentifiers(LogEvent logEvent)
        {
            // Do not assert on the version property
            // Do not assert on the service property
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.SerilogSpanIdKey));
            Assert.False(logEvent.Properties.ContainsKey(CorrelationIdentifier.SerilogTraceIdKey));
        }

        internal class LogEvent
        {
            public LogEvent(LogLevel logLevel, Exception exception, string message, Dictionary<string, object> properties)
            {
                LogLevel = logLevel;
                Exception = exception;
                Message = message;
                Properties = properties;
            }

            public LogLevel LogLevel { get; }

            public Exception Exception { get; }

            public string Message { get; }

            public Dictionary<string, object> Properties { get; }
        }

        private class InternalScopeLoggerProvider : ILoggerProvider, ILogger
        {
            private readonly List<LogEvent> _logEvents;
            private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

            public InternalScopeLoggerProvider(List<LogEvent> logEvents)
            {
                _logEvents = logEvents;
            }

            public void Dispose()
            {
            }

            public ILogger CreateLogger(string categoryName)
            {
                return this;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                var properties = new Dictionary<string, object>();
                _scopeProvider.ForEachScope(AddToProperties, properties);

                _logEvents.Add(
                    new LogEvent(
                        logLevel,
                        exception,
                        formatter(state, exception),
                        properties));

                void AddToProperties(object scope, Dictionary<string, object> builder)
                {
                    if (scope is IEnumerable<KeyValuePair<string, object>> scopeItems)
                    {
                        foreach (var (key, value) in scopeItems)
                        {
                            properties[key] = value;
                        }
                    }
                    else
                    {
                        var scopes = builder.TryGetValue("Scope", out var rawScope)
                                         ? (List<object>)rawScope
                                         : new List<object>();
                        scopes.Add(scope);
                        builder["Scope"] = scopes;
                    }
                }
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return _scopeProvider.Push(state);
            }
        }
    }
}
#endif
