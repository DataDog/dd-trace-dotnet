// <copyright file="NLogTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
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

#pragma warning disable 0618 // MDC and MDLC are obsolete

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class NLogTests : LogsInjectionTestBase
    {
        private const string CustomContextKey = "CustomContextKey";
        private const string CustomContextValue = "CustomContextValue";

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

        public enum ConfigurationType
        {
            /// <summary>
            /// No configuration provided at all.
            /// </summary>
            None,

            /// <summary>
            /// All targets in configuration will _not_ contain targets pre-configured with logs injection related elements.
            /// (e.g., "includeMdc = true" would be omitted from the JSON target)
            /// </summary>
            NoLogsInjection,

            /// <summary>
            /// All targets in configuration _will_ contain targets pre-configured with logs injection related elements.
            /// (e.g., "includeMdc = true" would be present in the JSON target)
            /// </summary>
            LogsInjection,

            /// <summary>
            /// Configuration file contains targets that are and aren't pre-configured with logs injection related elements.
            /// </summary>
            Both
        }

        public enum DirectLogSubmission
        {
            /// <summary>
            /// DirectLogSubmission is enabled.
            /// </summary>
            Enable,

            /// <summary>
            /// DirectLogSubmission is disabled.
            /// </summary>
            Disable
        }

        public enum Enable128BitInjection
        {
            /// <summary>
            /// Traces will be injected as 128-bit IDs.
            /// </summary>
            Enable,

            /// <summary>
            /// Traces will be injected as 64-bit IDs.
            /// </summary>
            Disable
        }

        public enum LoggingContext
        {
            /// <summary>
            /// No logging context.
            /// </summary>
            None,

            /// <summary>
            /// Use MDC as logging context.
            /// </summary>
            Mdc,

            /// <summary>
            /// Use MDLC as logging context.
            /// </summary>
            Mdlc,

            /// <summary>
            /// Use ScopeContext as logging context.
            /// </summary>
            ScopeContext
        }

        // The default sample uses NLog 6 on .NET Core and NLog 2.1 on .NET Framework.
        // PackageVersionData treats the default's empty version as matching every range,
        // so only compile the matching group when testing default samples.
#if !DEFAULT_SAMPLES || !NETFRAMEWORK
        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public Task InjectsLogsWhenEnabled_5_0_AndLater(
            [PackageVersionData(nameof(PackageVersions.NLog), minInclusive: "5.0.0")] string packageVersion,
            DirectLogSubmission enableLogShipping,
            [CombinatorialValues(LoggingContext.None, LoggingContext.Mdlc, LoggingContext.ScopeContext)] LoggingContext context,
            [CombinatorialValues(ConfigurationType.NoLogsInjection, ConfigurationType.LogsInjection, ConfigurationType.Both)] ConfigurationType configType,
            Enable128BitInjection enable128BitInjection)
            => InjectsLogsWhenEnabled(packageVersion, enableLogShipping, context, configType, enable128BitInjection);

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public Task DoesNotInjectLogsWhenDisabled_5_0_AndLater(
            [PackageVersionData(nameof(PackageVersions.NLog), minInclusive: "5.0.0")] string packageVersion,
            DirectLogSubmission enableLogShipping,
            [CombinatorialValues(LoggingContext.None, LoggingContext.Mdlc, LoggingContext.ScopeContext)] LoggingContext context,
            Enable128BitInjection enable128BitInjection)
            => DoesNotInjectLogsWhenDisabled(packageVersion, enableLogShipping, context, enable128BitInjection);

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public Task DirectlyShipsLogs_5_0_AndLater(
            [PackageVersionData(nameof(PackageVersions.NLog), minInclusive: "5.0.0")] string packageVersion,
            [CombinatorialValues(LoggingContext.None, LoggingContext.Mdlc, LoggingContext.ScopeContext)] LoggingContext context,
            ConfigurationType configType,
            Enable128BitInjection enable128BitInjection)
            => DirectlyShipsLogs(packageVersion, context, configType, enable128BitInjection);
#endif

        // NLog 4.6 and later support MDLC, but ScopeContext requires NLog 5.
#if !DEFAULT_SAMPLES
        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public Task InjectsLogsWhenEnabled_4_6_To_4_X(
            [PackageVersionData(nameof(PackageVersions.NLog), minInclusive: "4.6.0", maxInclusive: "4.*.*")] string packageVersion,
            DirectLogSubmission enableLogShipping,
            [CombinatorialValues(LoggingContext.None, LoggingContext.Mdlc)] LoggingContext context,
            [CombinatorialValues(ConfigurationType.NoLogsInjection, ConfigurationType.LogsInjection, ConfigurationType.Both)] ConfigurationType configType,
            Enable128BitInjection enable128BitInjection)
            => InjectsLogsWhenEnabled(packageVersion, enableLogShipping, context, configType, enable128BitInjection);

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public Task DoesNotInjectLogsWhenDisabled_4_6_To_4_X(
            [PackageVersionData(nameof(PackageVersions.NLog), minInclusive: "4.6.0", maxInclusive: "4.*.*")] string packageVersion,
            DirectLogSubmission enableLogShipping,
            [CombinatorialValues(LoggingContext.None, LoggingContext.Mdlc)] LoggingContext context,
            Enable128BitInjection enable128BitInjection)
            => DoesNotInjectLogsWhenDisabled(packageVersion, enableLogShipping, context, enable128BitInjection);

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public Task DirectlyShipsLogs_4_6_To_4_X(
            [PackageVersionData(nameof(PackageVersions.NLog), minInclusive: "4.6.0", maxInclusive: "4.*.*")] string packageVersion,
            [CombinatorialValues(LoggingContext.None, LoggingContext.Mdlc)] LoggingContext context,
            ConfigurationType configType,
            Enable128BitInjection enable128BitInjection)
            => DirectlyShipsLogs(packageVersion, context, configType, enable128BitInjection);
#endif

        // The 4.0–4.5 versions are only included in the minor-version matrix.
#if !DEFAULT_SAMPLES && TEST_ALL_MINOR_PACKAGE_VERSIONS
        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public Task InjectsLogsWhenEnabled_4_0_To_4_5(
            [PackageVersionData(nameof(PackageVersions.NLog), minInclusive: "4.0.0", maxInclusive: "4.5.*")] string packageVersion,
            DirectLogSubmission enableLogShipping,
            [CombinatorialValues(ConfigurationType.NoLogsInjection, ConfigurationType.LogsInjection, ConfigurationType.Both)] ConfigurationType configType,
            Enable128BitInjection enable128BitInjection)
            => InjectsLogsWhenEnabled(packageVersion, enableLogShipping, LoggingContext.None, configType, enable128BitInjection);

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public Task DoesNotInjectLogsWhenDisabled_4_0_To_4_5(
            [PackageVersionData(nameof(PackageVersions.NLog), minInclusive: "4.0.0", maxInclusive: "4.5.*")] string packageVersion,
            DirectLogSubmission enableLogShipping,
            Enable128BitInjection enable128BitInjection)
            => DoesNotInjectLogsWhenDisabled(packageVersion, enableLogShipping, LoggingContext.None, enable128BitInjection);

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public Task DirectlyShipsLogs_4_0_To_4_5(
            [PackageVersionData(nameof(PackageVersions.NLog), minInclusive: "4.0.0", maxInclusive: "4.5.*")] string packageVersion,
            ConfigurationType configType,
            Enable128BitInjection enable128BitInjection)
            => DirectlyShipsLogs(packageVersion, LoggingContext.None, configType, enable128BitInjection);
#endif

        // Pre-4.0 versions only run on .NET Framework and do not support JSON targets.
#if NETFRAMEWORK
        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public Task InjectsLogsWhenEnabled_Pre_4_0(
            [PackageVersionData(nameof(PackageVersions.NLog), maxInclusive: "3.*.*")] string packageVersion,
            DirectLogSubmission enableLogShipping,
            [CombinatorialValues(ConfigurationType.LogsInjection, ConfigurationType.Both)] ConfigurationType configType,
            Enable128BitInjection enable128BitInjection)
            => InjectsLogsWhenEnabled(packageVersion, enableLogShipping, LoggingContext.None, configType, enable128BitInjection);

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public Task DoesNotInjectLogsWhenDisabled_Pre_4_0(
            [PackageVersionData(nameof(PackageVersions.NLog), maxInclusive: "3.*.*")] string packageVersion,
            DirectLogSubmission enableLogShipping,
            Enable128BitInjection enable128BitInjection)
            => DoesNotInjectLogsWhenDisabled(packageVersion, enableLogShipping, LoggingContext.None, enable128BitInjection);

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public Task DirectlyShipsLogs_Pre_4_0(
            [PackageVersionData(nameof(PackageVersions.NLog), maxInclusive: "3.*.*")] string packageVersion,
            [CombinatorialValues(ConfigurationType.None, ConfigurationType.LogsInjection, ConfigurationType.Both)] ConfigurationType configType,
            Enable128BitInjection enable128BitInjection)
            => DirectlyShipsLogs(packageVersion, LoggingContext.None, configType, enable128BitInjection);
#endif

        private async Task InjectsLogsWhenEnabled(string packageVersion, DirectLogSubmission enableLogShipping, LoggingContext context, ConfigurationType configType, Enable128BitInjection enable128BitInjection)
        {
            SetEnvironmentVariable("DD_TRACE_128_BIT_TRACEID_LOGGING_ENABLED", enable128BitInjection == Enable128BitInjection.Enable ? "true" : "false");
            SetInstrumentationVerification();
            using var logsIntake = new MockLogsIntake();
            if (enableLogShipping == DirectLogSubmission.Enable)
            {
                EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.NLog), nameof(InjectsLogsWhenEnabled));
            }

            var expectedCorrelatedTraceCount = 1;
            var expectedCorrelatedSpanCount = 1;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion, arguments: string.Join(" ", new object[] { context, configType })))
            {
                var spans = await agent.WaitForSpansAsync(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

                var testFiles = GetTestFiles(packageVersion, true, configType);
                ValidateLogCorrelation(spans, testFiles, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion, use128Bits: enable128BitInjection == Enable128BitInjection.Enable);
                VerifyInstrumentation(processResult.Process);
                VerifyContextProperties(testFiles, packageVersion, context);
            }
        }

        private async Task DoesNotInjectLogsWhenDisabled(string packageVersion, DirectLogSubmission enableLogShipping, LoggingContext context, Enable128BitInjection enable128BitInjection)
        {
            const ConfigurationType configType = ConfigurationType.LogsInjection;

            SetEnvironmentVariable("DD_LOGS_INJECTION", "false");
            SetEnvironmentVariable("DD_TRACE_128_BIT_TRACEID_LOGGING_ENABLED", enable128BitInjection == Enable128BitInjection.Enable ? "true" : "false");
            SetInstrumentationVerification();
            using var logsIntake = new MockLogsIntake();
            if (enableLogShipping == DirectLogSubmission.Enable)
            {
                EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.NLog), nameof(InjectsLogsWhenEnabled));
            }

            var expectedCorrelatedTraceCount = 0;
            var expectedCorrelatedSpanCount = 0;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion, arguments: string.Join(" ", new object[] { context, configType })))
            {
                var spans = await agent.WaitForSpansAsync(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

                var testFiles = GetTestFiles(packageVersion, logsInjectionEnabled: false, configType);
                ValidateLogCorrelation(spans, testFiles, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion, disableLogCorrelation: true, use128Bits: enable128BitInjection == Enable128BitInjection.Enable);

                VerifyInstrumentation(processResult.Process);
                VerifyContextProperties(testFiles, packageVersion, context);
            }
        }

        private async Task DirectlyShipsLogs(string packageVersion, LoggingContext context, ConfigurationType configType, Enable128BitInjection enable128BitInjection)
        {
            var hostName = "integration_nlog_tests";
            using var logsIntake = new MockLogsIntake();

            SetInstrumentationVerification();
            SetEnvironmentVariable("DD_TRACE_128_BIT_TRACEID_LOGGING_ENABLED", enable128BitInjection == Enable128BitInjection.Enable ? "true" : "false");
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
            SetEnvironmentVariable("INCLUDE_CROSS_DOMAIN_CALL", "false");
            EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.NLog), hostName);

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion, arguments: string.Join(" ", new object[] { context, configType }));

            ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);

            var logs = logsIntake.Logs;

            using var scope = new AssertionScope();
            logs.Should().NotBeNull();
            logs.Should().HaveCountGreaterOrEqualTo(3);
            logs.Should()
                .OnlyContain(x => x.Service == "LogsInjection.NLog")
                .And.OnlyContain(x => x.Env == "integration_tests")
                .And.OnlyContain(x => x.Version == "1.0.0")
                .And.OnlyContain(x => x.Host == hostName)
                .And.OnlyContain(x => x.Source == "csharp")
                .And.OnlyContain(x => x.Exception == null)
                .And.OnlyContain(x => x.LogLevel == DirectSubmissionLogLevel.Information)
                .And.OnlyContain(x => x.TryGetProperty(NLogLogFormatter.LoggerNameKey).Exists);

            logs
               .Where(x => !x.Message.Contains(ExcludeMessagePrefix))
               .Should()
               .HaveCount(1)
               .And.OnlyContain(x => !string.IsNullOrEmpty(x.TraceId))
               .And.OnlyContain(x => !string.IsNullOrEmpty(x.SpanId));
            VerifyInstrumentation(processResult.Process);

            if (context != LoggingContext.None)
            {
                Func<MockLogsIntake.Log, string, string, bool> hasProperty = (log, key, value) =>
                {
                    var prop = log.TryGetProperty(key);
                    return prop.Exists && prop.Value == value;
                };
                logs.Should().Contain(x => hasProperty(x, CustomContextKey, CustomContextValue));
            }

            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.NLog);
        }

        private void VerifyContextProperties(LogFileTest[] testFiles, string packageVersion, LoggingContext context)
        {
            if (context == LoggingContext.None) { return; }

            // Skip for versions that don't support json
            foreach (var testFile in testFiles)
            {
                if (testFile.FileName.Contains("json"))
                {
                    var test = testFile; // jsonFile
                    var logFilePath = Path.Combine(EnvironmentHelper.GetSampleApplicationOutputDirectory(packageVersion), test.FileName);
                    var logs = GetLogFileContents(logFilePath);
                    foreach (var log in logs)
                    {
                        log.Should().MatchRegex(string.Format(test.RegexFormat, CustomContextKey, $@"""{CustomContextValue}"""));
                    }
                }
            }
        }

        private LogFileTest[] GetTestFiles(string packageVersion, bool logsInjectionEnabled = true, ConfigurationType configType = ConfigurationType.Both)
        {
            if (packageVersion is null or "")
            {
#if NETFRAMEWORK
                packageVersion = "2.1.0";
#else
                packageVersion = "4.5.0";
#endif
            }

            var version = new Version(packageVersion);

            if (version < new Version("4.0.0"))
            {
                // pre 4.0 can't write to json file
                if (configType == ConfigurationType.Both || configType == ConfigurationType.LogsInjection)
                {
                    return new[] { _textFileWithInjection };
                }
                else if (configType == ConfigurationType.NoLogsInjection)
                {
                    throw new Exception("NLog versions below 4.0.0 don't have JSON, so no automated logs injection");
                }
            }

            var unTracedLogType = logsInjectionEnabled switch
            {
                // When logs injection is enabled, untraced logs get env, service etc
                true => UnTracedLogTypes.EnvServiceTracingPropertiesOnly,
                // When logs injection is disabled, no enrichment
                false => UnTracedLogTypes.None
            };

            if (logsInjectionEnabled && configType == ConfigurationType.Both)
            {
                return new[] { _textFileWithInjection, GetJsonTestFile(unTracedLogType), GetJsonTestFileNoInjection(unTracedLogType) };
            }
            else if (logsInjectionEnabled && configType == ConfigurationType.LogsInjection)
            {
                return new[] { _textFileWithInjection, GetJsonTestFile(unTracedLogType) };
            }
            else if (logsInjectionEnabled && configType == ConfigurationType.NoLogsInjection)
            {
                return new[] {  GetJsonTestFileNoInjection(unTracedLogType) };
            }
            else
            {
                return new[] { _textFileWithInjection, GetJsonTestFile(unTracedLogType) };
            }
        }

        private LogFileTest GetJsonTestFile(UnTracedLogTypes unTracedLogType) => new()
        {
            FileName = "log-jsonFile-withInject.log",
            RegexFormat = @"""{0}"":\s*{1}",
            UnTracedLogTypes = unTracedLogType,
            PropertiesUseSerilogNaming = false
        };

        private LogFileTest GetJsonTestFileNoInjection(UnTracedLogTypes unTracedLogType) => new()
        {
            FileName = "log-jsonFile-noInject.log",
            RegexFormat = @"""{0}"":\s*{1}",
            UnTracedLogTypes = unTracedLogType,
            PropertiesUseSerilogNaming = false
        };
    }
}
