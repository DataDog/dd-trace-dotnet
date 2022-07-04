// <copyright file="SerilogTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
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

        public static IEnumerable<object[]> GetTestData()
        {
            foreach (var item in PackageVersions.Serilog)
            {
                yield return item.Concat(false);
                yield return item.Concat(true);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetTestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public void InjectsLogsWhenEnabled(string packageVersion, bool enableLogShipping)
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
            SetInstrumentationVerification();
            using var logsIntake = new MockLogsIntake();
            if (enableLogShipping)
            {
                EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.Serilog), nameof(InjectsLogsWhenEnabled));
            }

            var expectedCorrelatedTraceCount = 1;
            var expectedCorrelatedSpanCount = 1;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
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
        public void DoesNotInjectLogsWhenDisabled(string packageVersion, bool enableLogShipping)
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "false");
            SetInstrumentationVerification();
            using var logsIntake = new MockLogsIntake();
            if (enableLogShipping)
            {
                EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.Serilog), nameof(InjectsLogsWhenEnabled));
            }

            var expectedCorrelatedTraceCount = 0;
            var expectedCorrelatedSpanCount = 0;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

                var logFiles = GetLogFiles(packageVersion, logsInjectionEnabled: false);
                ValidateLogCorrelation(spans, logFiles, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion, disableLogCorrelation: true);
                VerifyInstrumentation(processResult.Process);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.Serilog), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public void DirectlyShipsLogs(string packageVersion)
        {
            var hostName = "integration_serilog_tests";
            using var logsIntake = new MockLogsIntake();

            SetInstrumentationVerification();
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
            EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.Serilog), hostName);

            var agentPort = TcpPortProvider.GetOpenPort();
            using var agent = MockTracerAgent.Create(agentPort);
            using var processResult = RunSampleAndWaitForExit(agent, packageVersion: packageVersion);

            Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode} and exception: {processResult.StandardError}");

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
               .NotBeEmpty()
               .And.OnlyContain(x => !string.IsNullOrEmpty(x.TraceId))
               .And.OnlyContain(x => !string.IsNullOrEmpty(x.SpanId));
            VerifyInstrumentation(processResult.Process);
        }

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
