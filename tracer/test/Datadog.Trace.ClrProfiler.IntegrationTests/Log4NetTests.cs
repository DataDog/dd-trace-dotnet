// <copyright file="Log4NetTests.cs" company="Datadog">
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
    public class Log4NetTests : LogsInjectionTestBase
    {
        private readonly LogFileTest[] _nlogPre205LogFileTests =
            {
                new LogFileTest()
                {
                    FileName = "log-textFile.log",
                    RegexFormat = @"{0}: {1}",
                    UnTracedLogTypes = UnTracedLogTypes.EmptyProperties,
                    PropertiesUseSerilogNaming = false
                }
            };

        private readonly LogFileTest[] _nlog205LogFileTests =
            {
                new LogFileTest()
                {
                    FileName = "log-textFile.log",
                    RegexFormat = @"{0}: {1}",
                    UnTracedLogTypes = UnTracedLogTypes.EmptyProperties,
                    PropertiesUseSerilogNaming = false
                },
                new LogFileTest()
                {
                    FileName = "log-jsonFile.log",
                    RegexFormat = @"""{0}"":{1}",
                    UnTracedLogTypes = UnTracedLogTypes.EmptyProperties,
                    PropertiesUseSerilogNaming = false
                }
            };

        public Log4NetTests(ITestOutputHelper output)
            : base(output, "LogsInjection.Log4Net")
        {
            SetServiceVersion("1.0.0");
        }

        public static System.Collections.Generic.IEnumerable<object[]> GetTestData()
        {
            return from item in PackageVersions.log4net
                   from callTarget in new[] { true, false }
                   from logShipping in new[] { true, false }
                   select item.Concat(callTarget, logShipping);
        }

        [SkippableTheory]
        [MemberData(nameof(GetTestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "LinuxUnsupported")]
        public void InjectsLogsWhenEnabled(string packageVersion, bool enableCallTarget, bool enableLogShipping)
        {
            SetCallTargetSettings(enableCallTarget);
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
            using var logsIntake = new MockLogsIntake();
            if (enableLogShipping)
            {
                EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationIds.Log4Net), nameof(InjectsLogsWhenEnabled));
            }

            int agentPort = TcpPortProvider.GetOpenPort();
            using (var agent = new MockTracerAgent(agentPort))
            using (RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

#if NETFRAMEWORK
                if (string.IsNullOrWhiteSpace(packageVersion) || new Version(packageVersion) >= new Version("2.0.5"))
                {
                    ValidateLogCorrelation(spans, _nlog205LogFileTests);
                }
                else
                {
                    ValidateLogCorrelation(spans, _nlogPre205LogFileTests);
                }
#else
                // Regardless of package version, for .NET Core just assert against raw log lines
                ValidateLogCorrelation(spans, _nlogPre205LogFileTests);
#endif
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetTestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "LinuxUnsupported")]
        public void DoesNotInjectLogsWhenDisabled(string packageVersion, bool enableCallTarget)
        {
            SetCallTargetSettings(enableCallTarget);
            SetEnvironmentVariable("DD_LOGS_INJECTION", "false");

            int agentPort = TcpPortProvider.GetOpenPort();
            using (var agent = new MockTracerAgent(agentPort))
            using (RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

#if NETFRAMEWORK
                if (string.IsNullOrWhiteSpace(packageVersion) || new Version(packageVersion) >= new Version("2.0.5"))
                {
                    ValidateLogCorrelation(spans, _nlog205LogFileTests, disableLogCorrelation: true);
                }
                else
                {
                    ValidateLogCorrelation(spans, _nlogPre205LogFileTests, disableLogCorrelation: true);
                }
#else
                // Regardless of package version, for .NET Core just assert against raw log lines
                ValidateLogCorrelation(spans, _nlogPre205LogFileTests, disableLogCorrelation: true);
#endif
            }
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.Serilog), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void DirectlyShipsLogs(string packageVersion)
        {
            var hostName = "integration_log4net_tests";
            using var logsIntake = new MockLogsIntake();

            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
            EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationIds.Log4Net), hostName);

            var agentPort = TcpPortProvider.GetOpenPort();
            using var agent = new MockTracerAgent(agentPort);
            using var processResult = RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion);

            Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode} and exception: {processResult.StandardError}");

            var logs = logsIntake.Logs;

            using var scope = new AssertionScope();
            logs.Should().NotBeNull();
            logs.Should().HaveCountGreaterOrEqualTo(3);
            logs.Should()
                .OnlyContain(x => x.Service == "LogsInjection.Log4Net")
                .And.OnlyContain(x => x.Host == hostName)
                .And.OnlyContain(x => x.Source == "csharp")
                .And.OnlyContain(x => x.Exception == null)
                .And.OnlyContain(x => x.LogLevel == DirectSubmissionLogLevel.Information);

            if (PackageSupportsLogsInjection(packageVersion))
            {
                logs
                   .Where(x => !x.Message.Contains(ExcludeMessagePrefix))
                   .Should()
                   .NotBeEmpty()
                   .And.OnlyContain(x => x.Env == "integration_tests")
                   .And.OnlyContain(x => x.Version == "1.0.0");
            }
        }

        private static bool PackageSupportsLogsInjection(string packageVersion)
            => string.IsNullOrWhiteSpace(packageVersion) || new Version(packageVersion) >= new Version("2.0.0");
    }
}
