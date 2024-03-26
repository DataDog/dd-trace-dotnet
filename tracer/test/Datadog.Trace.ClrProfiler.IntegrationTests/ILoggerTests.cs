// <copyright file="ILoggerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission.Formatting;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

// ReSharper disable InconsistentNaming
#pragma warning disable SA1402 // File may only contain a single type

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class ILoggerTests : ILoggerTestsBase
    {
        public ILoggerTests(ITestOutputHelper output)
            : base(output, "LogsInjection.ILogger")
        {
        }

        [SkippableTheory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task InjectsLogs(bool enableLogShipping)
        {
            await RunLogsInjectionTests(enableLogShipping, packageVersion: string.Empty);
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DirectlyShipsLogs(bool filterStartupLogs)
        {
            await RunDirectlyShipsLogs(filterStartupLogs, packageVersion: string.Empty);
        }
    }

#if NETFRAMEWORK || NET6_0_OR_GREATER
    public class ILoggerExtendedLoggerTests : ILoggerTestsBase
    {
        public ILoggerExtendedLoggerTests(ITestOutputHelper output)
            : base(output, "LogsInjection.ILogger.ExtendedLogger")
        {
        }

        public static IEnumerable<object[]> Data
            => from enableLogShipping in new[] { true, false }
               from packageVersion in PackageVersions.ILogger
               select new object[] { enableLogShipping, packageVersion[0] };

        [SkippableTheory]
        [MemberData(nameof(Data))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task InjectsLogs(bool enableLogShipping, string packageVersion)
        {
            await RunLogsInjectionTests(enableLogShipping, packageVersion);
        }

        [SkippableTheory]
        [MemberData(nameof(Data))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task DirectlyShipsLogs(bool filterStartupLogs, string packageVersion)
        {
            await RunDirectlyShipsLogs(filterStartupLogs, packageVersion);
        }
    }
#endif

    public class ILoggerTestsBase : LogsInjectionTestBase
    {
        private readonly string _serviceName;

        private readonly LogFileTest[] _logFiles =
        {
            new LogFileTest
            {
                FileName = "simple.log",
                RegexFormat = @"""{0}"":{1}",
                UnTracedLogTypes = UnTracedLogTypes.EnvServiceTracingPropertiesOnly,
                PropertiesUseSerilogNaming = true
            },
        };

        public ILoggerTestsBase(ITestOutputHelper output, string sampleName)
            : base(output, sampleName)
        {
            _serviceName = sampleName;
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
        }

        protected async Task RunLogsInjectionTests(bool enableLogShipping, string packageVersion)
        {
            // One of the traces starts by manual opening a span when the background service starts,
            // and then it sends a HTTP request to the server.
            // On .NET Framework, we do not yet automatically instrument AspNetCore so instead of
            // having one distributed trace, the result is two separate traces. So expect one more trace
            // when running on .NET Framework

            // We also log inside of the web server handling, so in .NET Core expect one more log line
#if NETFRAMEWORK
            var expectedCorrelatedTraceCount = 3;
            var expectedCorrelatedSpanCount = 3;
#else
            var expectedCorrelatedTraceCount = 2;
            var expectedCorrelatedSpanCount = 4;
#endif

            SetInstrumentationVerification();
            using var logsIntake = new MockLogsIntake();
            if (enableLogShipping)
            {
                EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.ILogger), "InjectsLogs");
            }

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion, aspNetCorePort: 0))
            {
                var spans = agent.WaitForSpans(1, 2500);
                spans.Should().HaveCountGreaterOrEqualTo(1);

                ValidateLogCorrelation(spans, _logFiles, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion: packageVersion);
                VerifyInstrumentation(processResult.Process);
            }
        }

        protected async Task RunDirectlyShipsLogs(bool filterStartupLogs, string packageVersion)
        {
            SetInstrumentationVerification();
            var hostName = "integration_ilogger_tests";
            using var logsIntake = new MockLogsIntake();

            EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.ILogger), hostName);
            if (filterStartupLogs)
            {
                SetEnvironmentVariable("Logging__Datadog__LogLevel__LogsInjection.ILogger.Startup", "Warning");
            }

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion, aspNetCorePort: 0);

            ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);

            var logs = logsIntake.Logs;

            var expectedLogCount = filterStartupLogs ? 7 : 12;
            using var scope = new AssertionScope();
            logs.Should().NotBeNull();
            logs.Should().HaveCountGreaterOrEqualTo(expectedLogCount); // have an unknown number of "Waiting for app started handling requests"
            logs.Should()
                .OnlyContain(x => x.Service == _serviceName)
                .And.OnlyContain(x => x.Host == hostName)
                .And.OnlyContain(x => x.Source == "csharp")
                .And.OnlyContain(x => x.Env == "integration_tests")
                .And.OnlyContain(x => x.Version == "1.0.0")
                .And.OnlyContain(x => x.Exception == null)
                .And.OnlyContain(x => x.LogLevel == DirectSubmissionLogLevel.Information)
                .And.OnlyContain(x => x.TryGetProperty(LoggerLogFormatter.LoggerNameKey).Exists);

            if (filterStartupLogs)
            {
                logs.Should().NotContain(x => x.Message.Contains("Building pipeline")); // these are filtered out
            }
            else
            {
                logs.Should().Contain(x => x.Message.Contains("Building pipeline")); // these should not be filtered out
            }

            logs.Where(x => !x.Message.Contains("Waiting for app started handling requests"))
                .Should()
                .HaveCount(expectedLogCount);

            VerifyInstrumentation(processResult.Process);
            telemetry.AssertIntegrationEnabled(IntegrationId.ILogger);
        }
    }
}
