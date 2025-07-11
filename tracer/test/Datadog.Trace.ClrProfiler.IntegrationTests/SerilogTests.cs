// <copyright file="SerilogTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class SerilogTests : LogsInjectionTestBase
    {
        private readonly LogFileTest _txtFile =
            new LogFileTest()
            {
                FileName = "log-textFile.log",
                RegexFormat = @"{0}: {1}",
                UnTracedLogTypes = UnTracedLogTypes.EmptyProperties,
                PropertiesUseSerilogNaming = true
            };

        public SerilogTests(ITestOutputHelper output)
            : base(output, "LogsInjection.Serilog")
        {
            SetServiceVersion("1.0.0");
        }

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task InjectsLogsWhenEnabled(
            [PackageVersionData(nameof(PackageVersions.Serilog), minInclusive: "2.12.0")] string packageVersion,
            bool enableLogShipping,
            bool loadFromConfig,
            bool enable128BitInjection)
        {
            // only include loadFromConfig when >= 2.12.0 (early versions of the config package are buggy)
            Skip.If(string.IsNullOrEmpty(packageVersion) && !EnvironmentHelper.IsCoreClr(), "Default version of Serilog for .NET Framework sample doesn't support load from config.");
            await InjectsLogsWhenEnabledBase(packageVersion, enableLogShipping, loadFromConfig, enable128BitInjection);
        }

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task DoesNotInjectLogsWhenDisabled(
            [PackageVersionData(nameof(PackageVersions.Serilog), minInclusive: "2.12.0")] string packageVersion,
            bool enableLogShipping,
            bool loadFromConfig,
            bool enable128BitInjection)
        {
            Skip.If(string.IsNullOrEmpty(packageVersion) && !EnvironmentHelper.IsCoreClr(), "Default version of Serilog for .NET Framework sample doesn't support load from config.");
            await DoesNotInjectLogsWhenDisabledBase(packageVersion, enableLogShipping, loadFromConfig, enable128BitInjection);
        }

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task DirectlyShipsLogs(
            [PackageVersionData(nameof(PackageVersions.Serilog), minInclusive: "2.12.0")] string packageVersion,
            bool loadFromConfig,
            bool enable128BitInjection)
        {
            Skip.If(string.IsNullOrEmpty(packageVersion) && !EnvironmentHelper.IsCoreClr(), "Default version of Serilog for .NET Framework sample doesn't support load from config.");
            await DirectlyShipsLogsBase(packageVersion, loadFromConfig, enable128BitInjection);
        }

#if NETFRAMEWORK
        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task InjectsLogsWhenEnabled_Pre_2_12_0(
            [PackageVersionData(nameof(PackageVersions.Serilog), maxInclusive: "2.11.*")] string packageVersion,
            bool enableLogShipping,
            bool enable128BitInjection)
        {
            // loadFromConfig = true and false are covered in 2.12.0 and later
            // loadFromConfig = false needs to be covered for anything below 2.12.0 - that is what this does
            await InjectsLogsWhenEnabledBase(packageVersion, enableLogShipping, loadFromConfig: false, enable128BitInjection);
        }

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task DoesNotInjectLogsWhenDisabled_Pre_2_12_0(
            [PackageVersionData(nameof(PackageVersions.Serilog), maxInclusive: "2.11.*")] string packageVersion,
            bool enableLogShipping,
            bool enable128BitInjection)
        {
            await DoesNotInjectLogsWhenDisabledBase(packageVersion, enableLogShipping, loadFromConfig: false, enable128BitInjection);
        }

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task DirectlyShipsLogs_Pre_2_12_0(
            [PackageVersionData(nameof(PackageVersions.Serilog), maxInclusive: "2.11.*")] string packageVersion,
            bool enable128BitInjection)
        {
            await DirectlyShipsLogsBase(packageVersion, loadFromConfig: false, enable128BitInjection);
        }
#endif

        private async Task InjectsLogsWhenEnabledBase(string packageVersion, bool enableLogShipping, bool loadFromConfig, bool enable128BitInjection)
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
            SetEnvironmentVariable("DD_TRACE_128_BIT_TRACEID_LOGGING_ENABLED", enable128BitInjection ? "true" : "false");
            SetSerilogConfiguration(loadFromConfig);
            SetInstrumentationVerification();
            using var logsIntake = new MockLogsIntake();
            if (enableLogShipping)
            {
                EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.Serilog), nameof(InjectsLogsWhenEnabled));
            }

            var expectedCorrelatedTraceCount = 1;
            var expectedCorrelatedSpanCount = 1;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

                var logFiles = GetLogFiles(packageVersion, logsInjectionEnabled: true);
                ValidateLogCorrelation(spans, logFiles, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion, use128Bits: enable128BitInjection);
                VerifyInstrumentation(processResult.Process);
            }
        }

        private async Task DoesNotInjectLogsWhenDisabledBase(string packageVersion, bool enableLogShipping, bool loadFromConfig, bool enable128BitInjection)
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "false");
            SetEnvironmentVariable("DD_TRACE_128_BIT_TRACEID_LOGGING_ENABLED", enable128BitInjection ? "true" : "false");
            SetSerilogConfiguration(loadFromConfig);
            SetInstrumentationVerification();
            using var logsIntake = new MockLogsIntake();
            if (enableLogShipping)
            {
                EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.Serilog), nameof(InjectsLogsWhenEnabled));
            }

            var expectedCorrelatedTraceCount = 0;
            var expectedCorrelatedSpanCount = 0;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

                var logFiles = GetLogFiles(packageVersion, logsInjectionEnabled: false);
                ValidateLogCorrelation(spans, logFiles, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion, disableLogCorrelation: true, use128Bits: enable128BitInjection);
                VerifyInstrumentation(processResult.Process);
            }
        }

        private async Task DirectlyShipsLogsBase(string packageVersion, bool loadFromConfig, bool enable128BitInjection)
        {
            var hostName = "integration_serilog_tests";
            using var logsIntake = new MockLogsIntake();

            SetInstrumentationVerification();
            SetSerilogConfiguration(loadFromConfig);
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
            SetEnvironmentVariable("INCLUDE_CROSS_DOMAIN_CALL", "false");
            SetEnvironmentVariable("DD_TRACE_128_BIT_TRACEID_LOGGING_ENABLED", enable128BitInjection ? "true" : "false");
            EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.Serilog), hostName);

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);

            ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);

            var logs = logsIntake.Logs;

            using var scope = new AssertionScope();
            logs.Should().NotBeNull();
            logs.Should().HaveCountGreaterOrEqualTo(3);
            logs.Should()
                .OnlyContain(x => x.Service == "LogsInjection.Serilog")
                .And.OnlyContain(x => x.Env == "integration_tests")
                .And.OnlyContain(x => x.Version == "1.0.0")
                .And.OnlyContain(x => x.Host == hostName)
                .And.OnlyContain(x => x.Source == "csharp")
                .And.OnlyContain(x => x.Exception == null)
                .And.OnlyContain(x => x.LogLevel == DirectSubmissionLogLevel.Information);

            logs
               .Where(x => !x.Message.Contains(ExcludeMessagePrefix))
               .Should()
               .HaveCount(1)
               .And.OnlyContain(x => !string.IsNullOrEmpty(x.TraceId))
               .And.OnlyContain(x => !string.IsNullOrEmpty(x.SpanId));
            VerifyInstrumentation(processResult.Process);
            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.Serilog);
        }

        private void SetSerilogConfiguration(bool loadFromConfig)
            => SetEnvironmentVariable("SERILOG_CONFIGURE_FROM_APPSETTINGS", loadFromConfig ? "1" : "0");

        private LogFileTest[] GetLogFiles(string packageVersion, bool logsInjectionEnabled)
        {
            var isPost200 =
#if NETCOREAPP
                // enabled in default version for .NET Core
                string.IsNullOrWhiteSpace(packageVersion) || new Version(packageVersion) >= new Version("2.0.0");
#else
                !string.IsNullOrWhiteSpace(packageVersion) && new Version(packageVersion) >= new Version("2.0.0");
#endif
            if (!isPost200)
            {
                // no json file, always the same format
                return new[] { _txtFile };
            }

            var unTracedLogFormat = logsInjectionEnabled
                                        ? UnTracedLogTypes.EnvServiceTracingPropertiesOnly
                                        : UnTracedLogTypes.None;

            var jsonFile = new LogFileTest()
            {
                FileName = "log-jsonFile.log",
                RegexFormat = @"""{0}"":{1}",
                UnTracedLogTypes = unTracedLogFormat,
                PropertiesUseSerilogNaming = true
            };

            return new[] { _txtFile, jsonFile };
        }
    }
}
