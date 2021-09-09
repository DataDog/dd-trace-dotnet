// <copyright file="Log4NetLogProviderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Datadog.Trace.Logging;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;
using Newtonsoft.Json;
using Xunit;

namespace Datadog.Trace.Tests.Logging
{
    [Collection(nameof(Datadog.Trace.Tests.Logging))]
    public class Log4NetLogProviderTests
    {
        private const string Log4NetExpectedStringFormat = "\"{0}\":\"{1}\"";
        private readonly ILogProvider _logProvider;
        private readonly ILog _logger;
        private readonly MemoryAppender _memoryAppender;

        public Log4NetLogProviderTests()
        {
            _memoryAppender = new MemoryAppender();
            var repository = log4net.LogManager.GetRepository(Assembly.GetAssembly(typeof(log4net.LogManager)));
            BasicConfigurator.Configure(repository, _memoryAppender);

            _logProvider = new CustomLog4NetLogProvider();
            LogProvider.SetCurrentLogProvider(_logProvider);
            _logger = new LoggerExecutionWrapper(_logProvider.GetLogger("Test"));
        }

        [Fact]
        public void LogsInjectionEnabledAddsParentCorrelationIdentifiers()
        {
            // Assert that the Log4Net log provider is correctly being used
            Assert.IsType<CustomLog4NetLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: true);
            LoggingProviderTestHelpers.LogInParentSpan(tracer, _logger, _logProvider.OpenMappedContext, out var parentScope, out var childScope);

            // Filter the logs
            List<LoggingEvent> filteredLogs = new List<LoggingEvent>(_memoryAppender.GetEvents());
            filteredLogs.RemoveAll(log => !log.MessageObject.ToString().Contains(LoggingProviderTestHelpers.LogPrefix));
            Assert.All(filteredLogs, e => LogEventContains(e, tracer.DefaultServiceName, tracer.Settings.ServiceVersion, tracer.Settings.Environment, parentScope));
        }

        [Fact]
        public void LogsInjectionEnabledAddsChildCorrelationIdentifiers()
        {
            // Assert that the Log4Net log provider is correctly being used
            Assert.IsType<CustomLog4NetLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: true);
            LoggingProviderTestHelpers.LogInChildSpan(tracer, _logger, _logProvider.OpenMappedContext, out var parentScope, out var childScope);

            // Filter the logs
            List<LoggingEvent> filteredLogs = new List<LoggingEvent>(_memoryAppender.GetEvents());
            filteredLogs.RemoveAll(log => !log.MessageObject.ToString().Contains(LoggingProviderTestHelpers.LogPrefix));
            Assert.All(filteredLogs, e => LogEventContains(e, tracer.DefaultServiceName, tracer.Settings.ServiceVersion, tracer.Settings.Environment, childScope));
        }

        [Fact]
        public void LogsInjectionEnabledDoesNotAddCorrelationIdentifiersOutsideSpans()
        {
            // Assert that the Log4Net log provider is correctly being used
            Assert.IsType<CustomLog4NetLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: true);
            LoggingProviderTestHelpers.LogOutsideSpans(tracer, _logger, _logProvider.OpenMappedContext, out var parentScope, out var childScope);

            // Filter the logs
            List<LoggingEvent> filteredLogs = new List<LoggingEvent>(_memoryAppender.GetEvents());
            filteredLogs.RemoveAll(log => !log.MessageObject.ToString().Contains(LoggingProviderTestHelpers.LogPrefix));
            Assert.All(filteredLogs, e => LogEventDoesNotContainCorrelationIdentifiers(e));
        }

        [Fact]
        public void LogsInjectionEnabledUsesTracerServiceName()
        {
            // Assert that the Log4Net log provider is correctly being used
            Assert.IsType<CustomLog4NetLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: true);
            LoggingProviderTestHelpers.LogInSpanWithServiceName(tracer, _logger, _logProvider.OpenMappedContext, "custom-service", out var scope);

            // Filter the logs
            List<LoggingEvent> filteredLogs = new List<LoggingEvent>(_memoryAppender.GetEvents());
            filteredLogs.RemoveAll(log => !log.MessageObject.ToString().Contains(LoggingProviderTestHelpers.LogPrefix));
            Assert.All(filteredLogs, e => LogEventContains(e, tracer.DefaultServiceName, tracer.Settings.ServiceVersion, tracer.Settings.Environment, scope));
        }

        [Fact]
        public void DisabledLibLogSubscriberDoesNotAddCorrelationIdentifiers()
        {
            // Assert that the Log4Net log provider is correctly being used
            Assert.IsType<CustomLog4NetLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: false);
            LoggingProviderTestHelpers.LogEverywhere(tracer, _logger, _logProvider.OpenMappedContext, out var parentScope, out var childScope);

            // Filter the logs
            List<LoggingEvent> filteredLogs = new List<LoggingEvent>(_memoryAppender.GetEvents());
            filteredLogs.RemoveAll(log => !log.MessageObject.ToString().Contains(LoggingProviderTestHelpers.LogPrefix));
            Assert.All(filteredLogs, e => LogEventDoesNotContainCorrelationIdentifiers(e));
        }

        internal static void LogEventContains(log4net.Core.LoggingEvent logEvent, string service, string version, string env, Scope scope)
        {
            LogEventContains(logEvent, service, version, env, scope.Span.TraceId, scope.Span.SpanId);
        }

        internal static void LogEventContains(log4net.Core.LoggingEvent logEvent, string service, string version, string env, ulong traceId, ulong spanId)
        {
            Assert.Contains(CorrelationIdentifier.ServiceKey, logEvent.Properties.GetKeys());
            Assert.Equal(service, logEvent.Properties[CorrelationIdentifier.ServiceKey].ToString());

            Assert.Contains(CorrelationIdentifier.VersionKey, logEvent.Properties.GetKeys());
            Assert.Equal(version, logEvent.Properties[CorrelationIdentifier.VersionKey].ToString());

            Assert.Contains(CorrelationIdentifier.EnvKey, logEvent.Properties.GetKeys());
            Assert.Equal(env, logEvent.Properties[CorrelationIdentifier.EnvKey].ToString());

            Assert.Contains(CorrelationIdentifier.TraceIdKey, logEvent.Properties.GetKeys());
            Assert.Equal(traceId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.TraceIdKey].ToString()));

            Assert.Contains(CorrelationIdentifier.SpanIdKey, logEvent.Properties.GetKeys());
            Assert.Equal(spanId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.SpanIdKey].ToString()));
        }

        internal static void LogEventContains(log4net.Core.LoggingEvent logEvent, ulong traceId, ulong spanId)
        {
            Assert.Contains(CorrelationIdentifier.TraceIdKey, logEvent.Properties.GetKeys());
            Assert.Equal(traceId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.TraceIdKey].ToString()));

            Assert.Contains(CorrelationIdentifier.SpanIdKey, logEvent.Properties.GetKeys());
            Assert.Equal(spanId, ulong.Parse(logEvent.Properties[CorrelationIdentifier.SpanIdKey].ToString()));
        }

        internal static void LogEventDoesNotContainCorrelationIdentifiers(log4net.Core.LoggingEvent logEvent)
        {
            if (logEvent.Properties.Contains(CorrelationIdentifier.SpanIdKey) &&
                logEvent.Properties.Contains(CorrelationIdentifier.TraceIdKey))
            {
                LogEventContains(logEvent, traceId: 0, spanId: 0);
            }
            else
            {
                Assert.DoesNotContain(CorrelationIdentifier.SpanIdKey, logEvent.Properties.GetKeys());
                Assert.DoesNotContain(CorrelationIdentifier.TraceIdKey, logEvent.Properties.GetKeys());
            }
        }

        /// <summary>
        /// Lightweight JSON-formatter for Log4Net inspired by https://github.com/Litee/log4net.Layout.Json
        /// </summary>
        internal class Log4NetJsonLayout : LayoutSkeleton
        {
            public override void ActivateOptions()
            {
            }

            public override void Format(TextWriter writer, LoggingEvent e)
            {
                var dic = new Dictionary<string, object>
                {
                    ["level"] = e.Level.DisplayName,
                    ["messageObject"] = e.MessageObject,
                    ["renderedMessage"] = e.RenderedMessage,
                    ["timestampUtc"] = e.TimeStamp.ToUniversalTime().ToString("O"),
                    ["logger"] = e.LoggerName,
                    ["thread"] = e.ThreadName,
                    ["exceptionObject"] = e.ExceptionObject,
                    ["exceptionObjectString"] = e.ExceptionObject == null ? null : e.GetExceptionString(),
                    ["userName"] = e.UserName,
                    ["domain"] = e.Domain,
                    ["identity"] = e.Identity,
                    ["location"] = e.LocationInformation.FullInfo,
                    ["properties"] = e.GetProperties()
                };
                writer.Write(JsonConvert.SerializeObject(dic));
            }
        }
    }
}
