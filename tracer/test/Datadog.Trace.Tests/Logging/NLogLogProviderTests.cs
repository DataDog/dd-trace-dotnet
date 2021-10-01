// <copyright file="NLogLogProviderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.LogProviders;
using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;
using NUnit.Framework;

namespace Datadog.Trace.Tests.Logging
{
    [NonParallelizable]
    public class NLogLogProviderTests
    {
        private const string NLogExpectedStringFormat = "\"{0}\": \"{1}\"";

        private CustomNLogLogProvider _logProvider;
        private ILog _logger;
        private MemoryTarget _target;

        [SetUp]
        public void Setup()
        {
            var config = new LoggingConfiguration();
            var layout = new JsonLayout();
            layout.IncludeMdc = true;
            layout.Attributes.Add(new JsonAttribute("time", Layout.FromString("${longdate}")));
            layout.Attributes.Add(new JsonAttribute("level", Layout.FromString("${level:uppercase=true}")));
            layout.Attributes.Add(new JsonAttribute("message", Layout.FromString("${message}")));
            _target = new MemoryTarget
            {
                Layout = layout
            };

            config.AddTarget("memory", _target);
            config.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Trace, _target));
            LogManager.Configuration = config;
            SimpleConfigurator.ConfigureForTargetLogging(_target, NLog.LogLevel.Trace);

            _logProvider = new CustomNLogLogProvider();
            LogProvider.SetCurrentLogProvider(_logProvider);
            _logger = new LoggerExecutionWrapper(_logProvider.GetLogger("test"));
        }

        [Test]
        public void LogsInjectionEnabledAddsParentCorrelationIdentifiers()
        {
            // Assert that the NLog log provider is correctly being used
            Assert.IsInstanceOf<CustomNLogLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: true);
            LoggingProviderTestHelpers.LogInParentSpan(tracer, _logger, _logProvider.OpenMappedContext, out var parentScope, out var childScope);

            // Filter the logs
            List<string> filteredLogs = new List<string>(_target.Logs);
            filteredLogs.RemoveAll(log => !log.Contains(LoggingProviderTestHelpers.LogPrefix));

            foreach (var log in filteredLogs)
            {
                LogEventContains(log, tracer.DefaultServiceName, tracer.Settings.ServiceVersion, tracer.Settings.Environment, parentScope);
            }
        }

        [Test]
        public void LogsInjectionEnabledAddsChildCorrelationIdentifiers()
        {
            // Assert that the NLog log provider is correctly being used
            Assert.IsInstanceOf<CustomNLogLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: true);
            LoggingProviderTestHelpers.LogInChildSpan(tracer, _logger, _logProvider.OpenMappedContext, out var parentScope, out var childScope);

            // Filter the logs
            List<string> filteredLogs = new List<string>(_target.Logs);
            filteredLogs.RemoveAll(log => !log.Contains(LoggingProviderTestHelpers.LogPrefix));

            foreach (var log in filteredLogs)
            {
                LogEventContains(log, tracer.DefaultServiceName, tracer.Settings.ServiceVersion, tracer.Settings.Environment, childScope);
            }
        }

        [Test]
        public void LogsInjectionEnabledDoesNotAddCorrelationIdentifiersOutsideSpans()
        {
            // Assert that the NLog log provider is correctly being used
            Assert.IsInstanceOf<CustomNLogLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: true);
            LoggingProviderTestHelpers.LogOutsideSpans(tracer, _logger, _logProvider.OpenMappedContext, out var parentScope, out var childScope);

            // Filter the logs
            List<string> filteredLogs = new List<string>(_target.Logs);
            filteredLogs.RemoveAll(log => !log.Contains(LoggingProviderTestHelpers.LogPrefix));

            foreach (var log in filteredLogs)
            {
                LogEventDoesNotContainCorrelationIdentifiers(log);
            }
        }

        [Test]
        public void LogsInjectionEnabledUsesTracerServiceName()
        {
            // Assert that the NLog log provider is correctly being used
            Assert.IsInstanceOf<CustomNLogLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: true);
            LoggingProviderTestHelpers.LogInSpanWithServiceName(tracer, _logger, _logProvider.OpenMappedContext, "custom-service", out var scope);

            // Filter the logs
            List<string> filteredLogs = new List<string>(_target.Logs);
            filteredLogs.RemoveAll(log => !log.Contains(LoggingProviderTestHelpers.LogPrefix));

            foreach (var log in filteredLogs)
            {
                LogEventContains(log, tracer.DefaultServiceName, tracer.Settings.ServiceVersion, tracer.Settings.Environment, scope);
            }
        }

        [Test]
        public void DisabledLibLogSubscriberDoesNotAddCorrelationIdentifiers()
        {
            // Assert that the NLog log provider is correctly being used
            Assert.IsInstanceOf<CustomNLogLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to TRUE
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection: false);
            LoggingProviderTestHelpers.LogEverywhere(tracer, _logger, _logProvider.OpenMappedContext, out var parentScope, out var childScope);

            // Filter the logs
            List<string> filteredLogs = new List<string>(_target.Logs);
            filteredLogs.RemoveAll(log => !log.Contains(LoggingProviderTestHelpers.LogPrefix));

            foreach (var log in filteredLogs)
            {
                LogEventDoesNotContainCorrelationIdentifiers(log);
            }
        }

        internal static void LogEventContains(string nLogString, string service, string version, string env, Scope scope)
        {
            StringAssert.Contains(string.Format(NLogExpectedStringFormat, CorrelationIdentifier.ServiceKey, service), nLogString);
            StringAssert.Contains(string.Format(NLogExpectedStringFormat, CorrelationIdentifier.EnvKey, env), nLogString);
            StringAssert.Contains(string.Format(NLogExpectedStringFormat, CorrelationIdentifier.VersionKey, version), nLogString);
            StringAssert.Contains(string.Format(NLogExpectedStringFormat, CorrelationIdentifier.SpanIdKey, scope.Span.SpanId), nLogString);
            StringAssert.Contains(string.Format(NLogExpectedStringFormat, CorrelationIdentifier.TraceIdKey, scope.Span.TraceId), nLogString);
        }

        internal static void LogEventDoesNotContainCorrelationIdentifiers(string nLogString)
        {
            // Do not assert on the service property
            Assert.True(
                nLogString.Contains(string.Format(NLogExpectedStringFormat, CorrelationIdentifier.SpanIdKey, 0)) ||
                !nLogString.Contains($"\"{CorrelationIdentifier.SpanIdKey}\""));
            Assert.True(
                nLogString.Contains(string.Format(NLogExpectedStringFormat, CorrelationIdentifier.TraceIdKey, 0)) ||
                !nLogString.Contains($"\"{CorrelationIdentifier.TraceIdKey}\""));
        }
    }
}
