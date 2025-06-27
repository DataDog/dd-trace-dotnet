// <copyright file="Log4NetTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Log4Net.DirectSubmission;
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
                   from logShipping in new[] { true, false }
                   from enable128BitInjection in new[] { true, false }
                   select new object[] { item[0], logShipping, enable128BitInjection };
        }

        [SkippableTheory]
        [MemberData(nameof(GetTestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task InjectsLogsWhenEnabled(string packageVersion, bool enableLogShipping, bool enable128BitInjection)
        {
            SetInstrumentationVerification();
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
            SetEnvironmentVariable("DD_TRACE_DEBUG", "true");
            SetEnvironmentVariable("DD_TRACE_128_BIT_TRACEID_LOGGING_ENABLED", enable128BitInjection ? "true" : "false");
            using var logsIntake = new MockLogsIntake();
            if (enableLogShipping)
            {
                EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.Log4Net), nameof(InjectsLogsWhenEnabled));
            }

            var expectedCorrelatedTraceCount = 1;
            var expectedCorrelatedSpanCount = 1;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                IImmutableList<MockSpan> spans = null;
                if (EnvironmentTools.IsWindows())
                {
                    spans = await agent.WaitForSpansAsync(1, 2500);
                }
                else if (!string.IsNullOrEmpty(packageVersion) && new Version(packageVersion) >= new Version("3.1.0"))
                {
                    // if we are not on Windows and we are above 3.1.0, an additional span is made
                    // from log4net to determine if we are on Android.
                    // This is a Process span, we can ultimately just ignore it
                    spans = await agent.WaitForSpansAsync(2, 2500);
                }
                else
                {
                    spans = await agent.WaitForSpansAsync(1, 2500);
                }

                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");
                // remove the Process span if it exists
                spans = spans.Where(s => s.Name != "command_execution").ToImmutableList();

#if NETFRAMEWORK
                if (!string.IsNullOrWhiteSpace(packageVersion) && new Version(packageVersion) >= new Version("2.0.5"))
                {
                    ValidateLogCorrelation(spans, _nlog205LogFileTests, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion, use128Bits: enable128BitInjection);
                }
                else
                {
                    ValidateLogCorrelation(spans, _nlogPre205LogFileTests, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion, use128Bits: enable128BitInjection);
                }
#else
                // Regardless of package version, for .NET Core just assert against raw log lines
                ValidateLogCorrelation(spans, _nlogPre205LogFileTests, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion, use128Bits: enable128BitInjection);
#endif
                VerifyInstrumentation(processResult.Process);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetTestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task DoesNotInjectLogsWhenDisabled(string packageVersion, bool enableLogShipping, bool enable128BitInjection)
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "false");
            SetEnvironmentVariable("DD_TRACE_128_BIT_TRACEID_LOGGING_ENABLED", enable128BitInjection ? "true" : "false");
            SetInstrumentationVerification();
            using var logsIntake = new MockLogsIntake();
            if (enableLogShipping)
            {
                EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.Log4Net), nameof(DoesNotInjectLogsWhenDisabled));
            }

            var expectedCorrelatedTraceCount = 0;
            var expectedCorrelatedSpanCount = 0;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                IImmutableList<MockSpan> spans = null;
                if (EnvironmentTools.IsWindows())
                {
                    spans = await agent.WaitForSpansAsync(1, 2500);
                }
                else if (!string.IsNullOrEmpty(packageVersion) && new Version(packageVersion) >= new Version("3.1.0"))
                {
                    // if we are not on Windows and we are above 3.1.0, an additional span is made
                    // from log4net to determine if we are on Android.
                    // This is a Process span, we can ultimately just ignore it
                    spans = await agent.WaitForSpansAsync(2, 2500);
                }
                else
                {
                    spans = await agent.WaitForSpansAsync(1, 2500);
                }

                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");
                // remove the Process span if it exists
                spans = spans.Where(s => s.Name != "command_execution").ToImmutableList();

#if NETFRAMEWORK
                if (!string.IsNullOrWhiteSpace(packageVersion) && new Version(packageVersion) >= new Version("2.0.5"))
                {
                    ValidateLogCorrelation(spans, _nlog205LogFileTests, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion, disableLogCorrelation: true, use128Bits: enable128BitInjection);
                }
                else
                {
                    ValidateLogCorrelation(spans, _nlogPre205LogFileTests, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion, disableLogCorrelation: true, use128Bits: enable128BitInjection);
                }
#else
                // Regardless of package version, for .NET Core just assert against raw log lines
                ValidateLogCorrelation(spans, _nlogPre205LogFileTests, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion, disableLogCorrelation: true, use128Bits: enable128BitInjection);
#endif
                VerifyInstrumentation(processResult.Process);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.log4net), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task DirectlyShipsLogs(string packageVersion)
        {
            var hostName = "integration_log4net_tests";
            using var logsIntake = new MockLogsIntake();

            SetInstrumentationVerification();
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
            SetEnvironmentVariable("INCLUDE_CROSS_DOMAIN_CALL", "false");
            EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.Log4Net), hostName);

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);

            ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);

            var logs = logsIntake.Logs;

            using var scope = new AssertionScope();
            logs.Should().NotBeNull();
            logs.Should().HaveCountGreaterOrEqualTo(3);
            logs.Should()
                .OnlyContain(x => x.Service == "LogsInjection.Log4Net")
                .And.OnlyContain(x => x.Env == "integration_tests")
                .And.OnlyContain(x => x.Version == "1.0.0")
                .And.OnlyContain(x => x.Host == hostName)
                .And.OnlyContain(x => x.Source == "csharp")
                .And.OnlyContain(x => x.Exception == null)
                .And.OnlyContain(x => x.LogLevel == DirectSubmissionLogLevel.Information)
                .And.OnlyContain(x => x.Tags.Contains(CommonTags.GitRepository))
                .And.OnlyContain(x => x.Tags.Contains(CommonTags.GitCommit))
                .And.OnlyContain(x => x.TryGetProperty(Log4NetLogFormatter.LoggerNameKey).Exists);

            if (PackageSupportsLogsInjection(packageVersion))
            {
                logs
                   .Where(x => !x.Message.Contains(ExcludeMessagePrefix))
                   .Should()
                   .NotBeEmpty()
                   // .HaveCount(1) // Currently fails
                   .And.OnlyContain(x => !string.IsNullOrEmpty(x.TraceId))
                   .And.OnlyContain(x => !string.IsNullOrEmpty(x.SpanId));
            }

            VerifyInstrumentation(processResult.Process);
            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.Log4Net);
        }

        private static bool PackageSupportsLogsInjection(string packageVersion)
            => string.IsNullOrWhiteSpace(packageVersion) || new Version(packageVersion) >= new Version("2.0.0");
    }
}
