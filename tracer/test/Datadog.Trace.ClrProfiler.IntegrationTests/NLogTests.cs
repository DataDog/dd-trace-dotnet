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

#if NETFRAMEWORK
#if NLOG_4_0
        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task InjectsLogsWhenEnabled_V4(
            [PackageVersionData(nameof(PackageVersions.NLog), minInclusive: "4.0.0")] string packageVersion,
            DirectLogSubmission enableLogShipping,
            [CombinatorialValues([LoggingContext.None, LoggingContext.Mdc, LoggingContext.Mdlc])] LoggingContext context,
            [CombinatorialValues([ConfigurationType.LogsInjection, ConfigurationType.NoLogsInjection, ConfigurationType.Both])] ConfigurationType configType,
            Enable128BitInjection enable128BitInjection)
        {
            await InjectsLogsWhenEnabledBase(packageVersion, enableLogShipping, context, configType, enable128BitInjection);
        }
#endif

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task InjectsLogsWhenEnabled_Pre_V4(
            [PackageVersionData(nameof(PackageVersions.NLog), maxInclusive: "3.*.*")] string packageVersion,
            DirectLogSubmission enableLogShipping,
            [CombinatorialValues([LoggingContext.None])] LoggingContext context,
            [CombinatorialValues([ConfigurationType.LogsInjection, ConfigurationType.Both])] ConfigurationType configType,
            Enable128BitInjection enable128BitInjection)
        {
            // NOTE:  I am unsure whether we need to fix the below issues or if they are fundamentally unsupported
            // FIXME: ConfigurationType.Mdc is failing for NLog versions < 4.0.0
            // FIXME: ConfigurationType.NoLogsInjection is failing for NLog versions < 4.0.0
            //        I think the issue is that we are using Mdlc instead of Mdc
            //        Omitting from the supported values for now
            // 09:11:02 [DBG]  StandardError:
            // 09:11:02[DBG]  System.ArgumentException: Invalid context property '{0}' for this NLog version
            // 09:11:02[DBG]  Parameter name: Mdlc
            // 09:11:02[DBG]     at LogsInjection.NLog.Program.AddToContextAndLog(String message, ContextProperty contextProperty)
            // 09:11:02[DBG]     at LogsInjection.NLog.Program.<> c__DisplayClass3_0.< Main > b__0(String message)
            // 09:11:02[DBG]     at PluginApplication.LoggingMethods.RunLoggingProcedure(Action`1 logAction)
            await InjectsLogsWhenEnabledBase(packageVersion, enableLogShipping, context, configType, enable128BitInjection);
        }

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task DoesNotInjectLogsWhenDisabled_Pre_V4_Point_6(
            [PackageVersionData(nameof(PackageVersions.NLog))] string packageVersion,
            DirectLogSubmission enableLogShipping,
            [CombinatorialValues([LoggingContext.None])] LoggingContext context, // FIXME: ConfigurationType.Mdc is failing for NLog versions < 4.0.0
            Enable128BitInjection enable128BitInjection)
        {
            using var logsIntake = await DoesNotInjectLogsWhenDisabledBase(packageVersion, enableLogShipping, context, enable128BitInjection);
        }

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task DirectlyShipsLogs_Pre_V4_Point_6(
            [PackageVersionData(nameof(PackageVersions.NLog))] string packageVersion,
            [CombinatorialValues([LoggingContext.None])] LoggingContext context,  // FIXME: ConfigurationType.Mdc is failing for NLog versions < 4.0.0 this is for logs injection
            [CombinatorialValues([ConfigurationType.LogsInjection, ConfigurationType.None])] ConfigurationType configType,
            Enable128BitInjection enable128BitInjection)
        {
            await DirectlyShipsLogsBase(packageVersion, context, configType, enable128BitInjection);
        }
#else
        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task InjectsLogsWhenEnabled_V5(
            [PackageVersionData(nameof(PackageVersions.NLog), minInclusive: "5.0.0")] string packageVersion,
            DirectLogSubmission enableLogShipping,
            LoggingContext context,
            [CombinatorialValues([ConfigurationType.LogsInjection, ConfigurationType.NoLogsInjection, ConfigurationType.Both])]ConfigurationType configType,
            Enable128BitInjection enable128BitInjection)
        {
            await InjectsLogsWhenEnabledBase(packageVersion, enableLogShipping, context, configType, enable128BitInjection);
        }

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task InjectsLogsWhenEnabled_V4_Point_6_And_Up(
            [PackageVersionData(nameof(PackageVersions.NLog), minInclusive: "4.6.0")] string packageVersion,
            DirectLogSubmission enableLogShipping,
            [CombinatorialValues([LoggingContext.None, LoggingContext.Mdc, LoggingContext.Mdlc])] LoggingContext context,
            [CombinatorialValues([ConfigurationType.LogsInjection, ConfigurationType.NoLogsInjection, ConfigurationType.Both])] ConfigurationType configType,
            Enable128BitInjection enable128BitInjection)
        {
            await InjectsLogsWhenEnabledBase(packageVersion, enableLogShipping, context, configType, enable128BitInjection);
        }

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task DoesNotInjectLogsWhenDisabled_V4_Point_6_And_Up(
            [PackageVersionData(nameof(PackageVersions.NLog), minInclusive: "4.6.0")] string packageVersion,
            DirectLogSubmission enableLogShipping,
            [CombinatorialValues([LoggingContext.None, LoggingContext.Mdc, LoggingContext.Mdlc])] LoggingContext context,
            Enable128BitInjection enable128BitInjection)
        {
            using var logsIntake = await DoesNotInjectLogsWhenDisabledBase(packageVersion, enableLogShipping, context, enable128BitInjection);
        }

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task DirectlyShipsLogs_V4_Point_6_And_Up(
            [PackageVersionData(nameof(PackageVersions.NLog), minInclusive: "4.6.0")] string packageVersion,
            [CombinatorialValues([LoggingContext.None, LoggingContext.Mdc, LoggingContext.Mdlc])] LoggingContext context,
            ConfigurationType configType,
            Enable128BitInjection enable128BitInjection)
        {
            Skip.If(packageVersion == "4.7.15" && context == LoggingContext.Mdc, "FIXME: missing injection");
            await DirectlyShipsLogsBase(packageVersion, context, configType, enable128BitInjection);
        }

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task DirectlyShipsLogs_V5(
            [PackageVersionData(nameof(PackageVersions.NLog), minInclusive: "5.0.0")] string packageVersion,
            LoggingContext context,
            ConfigurationType configType,
            Enable128BitInjection enable128BitInjection)
        {
            await DirectlyShipsLogsBase(packageVersion, context, configType, enable128BitInjection);
        }
#endif

        private async Task DirectlyShipsLogsBase(string packageVersion, LoggingContext context, ConfigurationType configType, Enable128BitInjection enable128BitInjection)
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

            telemetry.AssertIntegrationEnabled(IntegrationId.NLog);
        }

        private async Task<MockLogsIntake> DoesNotInjectLogsWhenDisabledBase(string packageVersion, DirectLogSubmission enableLogShipping, LoggingContext context, Enable128BitInjection enable128BitInjection)
        {
            var configType = ConfigurationType.LogsInjection;

            SetEnvironmentVariable("DD_LOGS_INJECTION", "false");
            SetEnvironmentVariable("DD_TRACE_128_BIT_TRACEID_LOGGING_ENABLED", enable128BitInjection == Enable128BitInjection.Enable ? "true" : "false");
            SetInstrumentationVerification();
            var logsIntake = new MockLogsIntake();
            if (enableLogShipping == DirectLogSubmission.Enable)
            {
                EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.NLog), "InjectsLogsWhenEnabled");
            }

            var expectedCorrelatedTraceCount = 0;
            var expectedCorrelatedSpanCount = 0;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion, arguments: string.Join(" ", new object[] { context, configType })))
            {
                var spans = agent.WaitForSpans(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

                var testFiles = GetTestFiles(packageVersion, logsInjectionEnabled: false, configType);
                ValidateLogCorrelation(spans, testFiles, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion, disableLogCorrelation: true, use128Bits: enable128BitInjection == Enable128BitInjection.Enable);

                VerifyInstrumentation(processResult.Process);
                VerifyContextProperties(testFiles, packageVersion, context);
            }

            return logsIntake;
        }

        private async Task InjectsLogsWhenEnabledBase(
            string packageVersion,
            DirectLogSubmission enableLogShipping,
            LoggingContext context,
            ConfigurationType configType,
            Enable128BitInjection enable128BitInjection)
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
            SetEnvironmentVariable("DD_TRACE_128_BIT_TRACEID_LOGGING_ENABLED", enable128BitInjection == Enable128BitInjection.Enable ? "true" : "false");
            SetInstrumentationVerification();
            using var logsIntake = new MockLogsIntake();
            if (enableLogShipping == DirectLogSubmission.Enable)
            {
                EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.NLog), "InjectsLogsWhenEnabled");
            }

            var expectedCorrelatedTraceCount = 1;
            var expectedCorrelatedSpanCount = 1;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion, arguments: string.Join(" ", new object[] { context, configType })))
            {
                var spans = agent.WaitForSpans(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

                var testFiles = GetTestFiles(packageVersion, true, configType);
                ValidateLogCorrelation(spans, testFiles, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion, use128Bits: enable128BitInjection == Enable128BitInjection.Enable);
                VerifyInstrumentation(processResult.Process);
                VerifyContextProperties(testFiles, packageVersion, context);
            }
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
            RegexFormat = @"""{0}"": {1}",
            UnTracedLogTypes = unTracedLogType,
            PropertiesUseSerilogNaming = false
        };

        private LogFileTest GetJsonTestFileNoInjection(UnTracedLogTypes unTracedLogType) => new()
        {
            FileName = "log-jsonFile-noInject.log",
            RegexFormat = @"""{0}"": {1}",
            UnTracedLogTypes = unTracedLogType,
            PropertiesUseSerilogNaming = false
        };
    }
}
