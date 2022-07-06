// <copyright file="NLogTests.cs" company="Datadog">
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
    public class NLogTests : LogsInjectionTestBase
    {
        private readonly LogFileTest _textFile = new()
        {
            FileName = "log-textFile.log",
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

        public static IEnumerable<object[]> GetTestData()
        {
            foreach (var item in PackageVersions.NLog)
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
                EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.NLog), nameof(InjectsLogsWhenEnabled));
            }

            var expectedCorrelatedTraceCount = 1;
            var expectedCorrelatedSpanCount = 1;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

                var testFiles = GetTestFiles(packageVersion);
                ValidateLogCorrelation(spans, testFiles, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion);
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
                EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.NLog), nameof(InjectsLogsWhenEnabled));
            }

            var expectedCorrelatedTraceCount = 0;
            var expectedCorrelatedSpanCount = 0;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

                var testFiles = GetTestFiles(packageVersion, logsInjectionEnabled: false);
                ValidateLogCorrelation(spans, testFiles, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion, disableLogCorrelation: true);

                VerifyInstrumentation(processResult.Process);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.NLog), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public void DirectlyShipsLogs(string packageVersion)
        {
            var hostName = "integration_nlog_tests";
            using var logsIntake = new MockLogsIntake();

            SetInstrumentationVerification();
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
            EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.NLog), hostName);

            var agentPort = TcpPortProvider.GetOpenPort();
            using var agent = MockTracerAgent.Create(agentPort);
            using var processResult = RunSampleAndWaitForExit(agent, packageVersion: packageVersion);

            Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode} and exception: {processResult.StandardError}");

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
                .And.OnlyContain(x => x.LogLevel == DirectSubmissionLogLevel.Information);

            logs
               .Where(x => !x.Message.Contains(ExcludeMessagePrefix))
               .Should()
               .NotBeEmpty()
               .And.OnlyContain(x => !string.IsNullOrEmpty(x.TraceId))
               .And.OnlyContain(x => !string.IsNullOrEmpty(x.SpanId));
            VerifyInstrumentation(processResult.Process);
        }

        private LogFileTest[] GetTestFiles(string packageVersion, bool logsInjectionEnabled = true)
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
                return new[] { _textFile };
            }

            var unTracedLogType = logsInjectionEnabled switch
            {
                // When logs injection is enabled, untraced logs get env, service etc
                true => UnTracedLogTypes.EnvServiceTracingPropertiesOnly,
                // When logs injection is enabled, no enrichment
                false => UnTracedLogTypes.None,
            };

            return new[] { _textFile, GetJsonTestFile(unTracedLogType) };
        }

        private LogFileTest GetJsonTestFile(UnTracedLogTypes unTracedLogType) => new()
        {
            FileName = "log-jsonFile.log",
            RegexFormat = @"""{0}"": {1}",
            UnTracedLogTypes = unTracedLogType,
            PropertiesUseSerilogNaming = false
        };
    }
}
