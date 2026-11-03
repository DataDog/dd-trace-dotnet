// <copyright file="NLogTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Formatting;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class NLogTests : LogsInjectionTestBase
    {
        private const string CustomContextKey = "CustomContextKey";
        private const string CustomContextValue = "CustomContextValue";
        private const string HostName = "integration_nlog_tests";
        private const string JsonFileWithInjection = "log-jsonFile-withInject.log";
        private const string JsonFileNoInjection = "log-jsonFile-noInject.log";

        // The sample logs every message once per supported context, with the context appended as "(context: [name])"
        private static readonly Regex LoggingContextRegex = new(@"\(context: (\w+)\)");

        private readonly LogFileTest _textFileWithInjection = new()
        {
            FileName = "log-textFile-withInject.log",
            RegexFormat = @"{0}: {1}",
            // txt format can't conditionally add properties
            UnTracedLogTypes = UnTracedLogTypes.EmptyProperties,
            PropertiesUseSerilogNaming = false
        };

        public NLogTests(ITestOutputHelper output)
            : base(output, "LogsInjection.NLog")
        {
            SetServiceVersion("1.0.0");
        }

        private enum ConfigurationType
        {
            /// <summary>
            /// No configuration provided at all.
            /// </summary>
            None,

            /// <summary>
            /// Configuration file contains targets that are and aren't pre-configured with logs injection related elements.
            /// (e.g., "includeMdc = true" is present in one JSON target, and omitted from the other)
            /// </summary>
            Both
        }

        /// <summary>
        /// The contexts the sample uses when logging, which depend on the version of NLog
        /// </summary>
        private enum LoggingContext
        {
            /// <summary>
            /// No logging context.
            /// </summary>
            None,

            /// <summary>
            /// Use MDLC as logging context.
            /// </summary>
            Mdlc,

            /// <summary>
            /// Use ScopeContext as logging context.
            /// </summary>
            ScopeContext
        }

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task InjectsLogsWhenEnabled(
            [PackageVersionData(nameof(PackageVersions.NLog))] string packageVersion,
            bool enableLogShipping,
            bool enable128BitInjection)
        {
            SetEnvironmentVariable("DD_TRACE_128_BIT_TRACEID_LOGGING_ENABLED", enable128BitInjection ? "true" : "false");
            SetInstrumentationVerification();
            using var logsIntake = new MockLogsIntake();
            using var telemetry = enableLogShipping ? EnableLogShipping(logsIntake) : null;

            var expectedCorrelatedTraceCount = 1;
            var expectedCorrelatedSpanCount = 1;
            var expectedContexts = GetExpectedContexts(packageVersion);

            using var agent = EnvironmentHelper.GetMockAgent();
            using var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion, arguments: nameof(ConfigurationType.Both));

            var spans = await agent.WaitForSpansAsync(1, 2500);
            spans.Should().NotBeEmpty();

            var testFiles = GetTestFiles(packageVersion, logsInjectionEnabled: true);
            ValidateLogCorrelation(spans, testFiles, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion, use128Bits: enable128BitInjection);
            VerifyInstrumentation(processResult.Process);
            VerifyContextProperties(testFiles, packageVersion, expectedContexts, logsInjectionEnabled: true);

            if (telemetry is not null)
            {
                AssertShippedLogs(logsIntake.Logs, expectedContexts, spans, enable128BitInjection);
                await telemetry.AssertIntegrationEnabledAsync(IntegrationId.NLog);
            }
        }

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task DoesNotInjectLogsWhenDisabled(
            [PackageVersionData(nameof(PackageVersions.NLog))] string packageVersion,
            bool enableLogShipping)
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "false");
            SetInstrumentationVerification();
            using var logsIntake = new MockLogsIntake();
            using var telemetry = enableLogShipping ? EnableLogShipping(logsIntake) : null;

            var expectedCorrelatedTraceCount = 0;
            var expectedCorrelatedSpanCount = 0;
            var expectedContexts = GetExpectedContexts(packageVersion);

            using var agent = EnvironmentHelper.GetMockAgent();
            using var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion, arguments: nameof(ConfigurationType.Both));

            var spans = await agent.WaitForSpansAsync(1, 2500);
            spans.Should().NotBeEmpty();

            var testFiles = GetTestFiles(packageVersion, logsInjectionEnabled: false);
            ValidateLogCorrelation(spans, testFiles, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion, disableLogCorrelation: true);
            VerifyInstrumentation(processResult.Process);
            VerifyContextProperties(testFiles, packageVersion, expectedContexts, logsInjectionEnabled: false);

            if (telemetry is not null)
            {
                // logs injection is disabled, so the shipped logs aren't correlated
                AssertShippedLogs(logsIntake.Logs, expectedContexts, correlatedSpans: null, use128Bits: false);
                await telemetry.AssertIntegrationEnabledAsync(IntegrationId.NLog);
            }
        }

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task DirectlyShipsLogs(
            [PackageVersionData(nameof(PackageVersions.NLog))] string packageVersion)
        {
            // Shipping logs with a configuration file is covered by InjectsLogsWhenEnabled,
            // this covers the case where there's no NLog configuration at all, so we create one
            SetInstrumentationVerification();
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
            using var logsIntake = new MockLogsIntake();
            using var telemetry = EnableLogShipping(logsIntake);

            using var agent = EnvironmentHelper.GetMockAgent();
            using var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion, arguments: nameof(ConfigurationType.None));

            var spans = await agent.WaitForSpansAsync(1, 2500);
            spans.Should().NotBeEmpty();

            AssertShippedLogs(logsIntake.Logs, GetExpectedContexts(packageVersion), spans, use128Bits: true);
            VerifyInstrumentation(processResult.Process);
            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.NLog);
        }

        private static Version GetNLogVersion(string packageVersion)
        {
            if (packageVersion is null or "")
            {
                // The default versions used by the sample, see ApiVersion in LogsInjection.NLog.csproj
                return EnvironmentHelper.IsCoreClr() ? new Version("6.0.0") : new Version("2.1.0");
            }

            return new Version(packageVersion);
        }

        private static LoggingContext[] GetExpectedContexts(string packageVersion)
        {
            var version = GetNLogVersion(packageVersion);
            if (version >= new Version("5.0.0"))
            {
                return [LoggingContext.None, LoggingContext.Mdlc, LoggingContext.ScopeContext];
            }

            if (version >= new Version("4.6.0"))
            {
                return [LoggingContext.None, LoggingContext.Mdlc];
            }

            return [LoggingContext.None];
        }

        private static LoggingContext GetLoggingContext(string message)
        {
            var match = LoggingContextRegex.Match(message);
            if (!match.Success)
            {
                throw new Exception($"Log message did not include the logging context: {message}");
            }

            return (LoggingContext)Enum.Parse(typeof(LoggingContext), match.Groups[1].Value);
        }

        private static LogFileTest GetJsonTestFile(string fileName, UnTracedLogTypes unTracedLogType) => new()
        {
            FileName = fileName,
            RegexFormat = @"""{0}"":\s*{1}",
            UnTracedLogTypes = unTracedLogType,
            PropertiesUseSerilogNaming = false
        };

        private MockTelemetryAgent EnableLogShipping(MockLogsIntake logsIntake)
        {
            // Calls across AppDomains (on .NET Framework) result in duplicate telemetry, which breaks the telemetry assertions
            SetEnvironmentVariable("INCLUDE_CROSS_DOMAIN_CALL", "false");
            EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.NLog), HostName);
            return this.ConfigureTelemetry();
        }

        private void AssertShippedLogs(IReadOnlyCollection<MockLogsIntake.Log> logs, LoggingContext[] expectedContexts, IReadOnlyCollection<MockSpan> correlatedSpans, bool use128Bits)
        {
            using var scope = new AssertionScope();
            logs.Should().NotBeNull();
            logs.Should().HaveCountGreaterOrEqualTo(3 * expectedContexts.Length);
            logs.Should()
                .OnlyContain(x => x.Service == "LogsInjection.NLog")
                .And.OnlyContain(x => x.Env == "integration_tests")
                .And.OnlyContain(x => x.Version == "1.0.0")
                .And.OnlyContain(x => x.Host == HostName)
                .And.OnlyContain(x => x.Source == "csharp")
                .And.OnlyContain(x => x.Exception == null)
                .And.OnlyContain(x => x.LogLevel == DirectSubmissionLogLevel.Information)
                .And.OnlyContain(x => x.TryGetProperty(NLogLogFormatter.LoggerNameKey).Exists);

            // the message inside the span should be logged exactly once per context
            var tracedLogs = logs.Where(x => !x.Message.Contains(ExcludeMessagePrefix)).ToList();
            tracedLogs.Select(x => GetLoggingContext(x.Message)).Should().BeEquivalentTo(expectedContexts);

            foreach (var log in logs)
            {
                var property = log.TryGetProperty(CustomContextKey);
                if (GetLoggingContext(log.Message) == LoggingContext.None)
                {
                    property.Exists.Should().BeFalse($"'{log.Message}' should not have the custom context property");
                }
                else
                {
                    property.Exists.Should().BeTrue($"'{log.Message}' should have the custom context property");
                    property.Value.Should().Be(CustomContextValue);
                }
            }

            if (correlatedSpans is null)
            {
                logs.Should()
                    .OnlyContain(x => string.IsNullOrEmpty(x.TraceId))
                    .And.OnlyContain(x => string.IsNullOrEmpty(x.SpanId));
                return;
            }

            var traceIds = GetExpectedLogTraceIds(correlatedSpans, use128Bits);
            var spanIds = correlatedSpans.Select(x => x.SpanId.ToString()).ToList();

            tracedLogs.Should()
                      .OnlyContain(x => traceIds.Contains(x.TraceId))
                      .And.OnlyContain(x => spanIds.Contains(x.SpanId));
            tracedLogs.Select(x => x.SpanId).Distinct().Should().ContainSingle();
            logs.Where(x => x.Message.Contains(ExcludeMessagePrefix))
                .Should()
                .OnlyContain(x => string.IsNullOrEmpty(x.TraceId))
                .And.OnlyContain(x => string.IsNullOrEmpty(x.SpanId));
        }

        private void VerifyContextProperties(LogFileTest[] testFiles, string packageVersion, LoggingContext[] expectedContexts, bool logsInjectionEnabled)
        {
            // txt format doesn't include the context properties
            foreach (var testFile in testFiles.Where(x => x.FileName.Contains("json")))
            {
                // The noInject target only includes the context properties when we configure it for logs injection
                var includesContextProperties = logsInjectionEnabled || testFile.FileName == JsonFileWithInjection;
                var contextPropertyRegex = string.Format(testFile.RegexFormat, CustomContextKey, $@"""{CustomContextValue}""");

                var logFilePath = Path.Combine(EnvironmentHelper.GetSampleApplicationOutputDirectory(packageVersion), testFile.FileName);
                var logs = GetLogFileContents(logFilePath);

                using var scope = new AssertionScope(testFile.FileName);
                logs.Select(GetLoggingContext).Distinct().Should().BeEquivalentTo(expectedContexts);
                foreach (var log in logs)
                {
                    if (includesContextProperties && GetLoggingContext(log) != LoggingContext.None)
                    {
                        log.Should().MatchRegex(contextPropertyRegex);
                    }
                    else
                    {
                        log.Should().NotContain(CustomContextKey);
                    }
                }
            }
        }

        private LogFileTest[] GetTestFiles(string packageVersion, bool logsInjectionEnabled)
        {
            if (GetNLogVersion(packageVersion) < new Version("4.0.0"))
            {
                // pre 4.0 can't write to json file
                return [_textFileWithInjection];
            }

            var unTracedLogType = logsInjectionEnabled switch
            {
                // When logs injection is enabled, untraced logs get env, service etc
                true => UnTracedLogTypes.EnvServiceTracingPropertiesOnly,
                // When logs injection is disabled, no enrichment
                false => UnTracedLogTypes.None
            };

            return [_textFileWithInjection, GetJsonTestFile(JsonFileWithInjection, unTracedLogType), GetJsonTestFile(JsonFileNoInjection, unTracedLogType)];
        }
    }
}
