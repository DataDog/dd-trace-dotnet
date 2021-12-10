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
using Xunit;

namespace Datadog.Trace.Tests.Logging
{
    [Collection(nameof(Datadog.Trace.Tests.Logging))]
    [TestCaseOrderer("Datadog.Trace.TestHelpers.AlphabeticalOrderer", "Datadog.Trace.TestHelpers")]
    public class NLogLogProviderTests
    {
        private const string NLogExpectedStringFormat = "\"{0}\": \"{1}\"";

        private readonly ILogProvider _logProvider;
        private readonly ILog _logger;
        private readonly MemoryTarget _target;

        public NLogLogProviderTests()
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

            _logProvider = new NoOpNLogLogProvider();
            LogProvider.SetCurrentLogProvider(_logProvider);
            _logger = new LoggerExecutionWrapper(_logProvider.GetLogger("test"));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DoesNotAddCorrelationIdentifiers(bool enableLogsInjection)
        {
            // Assert that the NLog log provider is correctly being used
            Assert.IsType<NoOpNLogLogProvider>(LogProvider.CurrentLogProvider);

            // Instantiate a tracer for this test with default settings and set LogsInjectionEnabled to the test case value
            var tracer = LoggingProviderTestHelpers.InitializeTracer(enableLogsInjection);
            LoggingProviderTestHelpers.LogEverywhere(tracer, _logger, _logProvider.OpenMappedContext, out var parentScope, out var childScope);

            // Filter the logs
            List<string> filteredLogs = new List<string>(_target.Logs);
            filteredLogs.RemoveAll(log => !log.ToString().Contains(LoggingProviderTestHelpers.LogPrefix));
            Assert.All(filteredLogs, e => LogEventDoesNotContainCorrelationIdentifiers(e));
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
