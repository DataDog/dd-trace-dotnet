// <copyright file="SerilogTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        // exclude loadFromConfig from v1.x as it's not available
        public static IEnumerable<object[]> GetTestData()
            => from packageVersion in PackageVersions.Serilog.SelectMany(x => x).Select(x => (string)x)
               from enableLogShipping in new[] { true, false }
               from loadFromConfig in new[] { true, false }
               where !loadFromConfig // only include loadFromConfig when >= 2.12.0 (early versions of the config package are buggy)
                  || (!string.IsNullOrEmpty(packageVersion) && new Version(packageVersion) >= new Version("2.12.0"))
               select new object[] { packageVersion, enableLogShipping, loadFromConfig };

        [SkippableTheory]
        [MemberData(nameof(GetTestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task InjectsLogsWhenEnabled(string packageVersion, bool enableLogShipping, bool loadFromConfig)
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
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
                var spans = agent.WaitForSpans(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

                var logFiles = GetLogFiles(packageVersion, logsInjectionEnabled: true);
                ValidateLogCorrelation(spans, logFiles, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion);
                VerifyInstrumentation(processResult.Process);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetTestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task DoesNotInjectLogsWhenDisabled(string packageVersion, bool enableLogShipping, bool loadFromConfig)
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "false");
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
                var spans = agent.WaitForSpans(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

                var logFiles = GetLogFiles(packageVersion, logsInjectionEnabled: false);
                ValidateLogCorrelation(spans, logFiles, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion, disableLogCorrelation: true);
                VerifyInstrumentation(processResult.Process);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetTestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task DirectlyShipsLogs(string packageVersion, bool enableLogShipping, bool loadFromConfig)
        {
            if (!enableLogShipping)
            {
                // invalid config, just easier than creating another test data configuration
                return;
            }

            var hostName = "integration_serilog_tests";
            using var logsIntake = new MockLogsIntake();

            SetInstrumentationVerification();
            SetSerilogConfiguration(loadFromConfig);
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
            SetEnvironmentVariable("INCLUDE_CROSS_DOMAIN_CALL", "false");
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
            telemetry.AssertIntegrationEnabled(IntegrationId.Serilog);
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
