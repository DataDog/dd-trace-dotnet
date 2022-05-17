// <copyright file="ILoggerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
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
    // ReSharper disable once InconsistentNaming
    public class ILoggerTests : LogsInjectionTestBase
    {
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

        public ILoggerTests(ITestOutputHelper output)
            : base(output, "LogsInjection.ILogger")
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
        }

        [SkippableTheory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public void InjectsLogs(bool enableLogShipping)
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
                EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.ILogger), nameof(InjectsLogs));
            }

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = RunSampleAndWaitForExit(agent, aspNetCorePort: 0))
            {
                var spans = agent.WaitForSpans(1, 2500);
                spans.Should().HaveCountGreaterOrEqualTo(1);

                ValidateLogCorrelation(spans, _logFiles, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount);
                VerifyInstrumentation(processResult.Process);
            }
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public void DirectlyShipsLogs()
        {
            SetInstrumentationVerification();
            var hostName = "integration_ilogger_tests";
            using var logsIntake = new MockLogsIntake();

            EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.ILogger), hostName);

            var agentPort = TcpPortProvider.GetOpenPort();
            using var agent = MockTracerAgent.Create(agentPort);
            using var processResult = RunSampleAndWaitForExit(agent, aspNetCorePort: 0);

            Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode} and exception: {processResult.StandardError}");

            var logs = logsIntake.Logs;

            using var scope = new AssertionScope();
            logs.Should().NotBeNull();
            logs.Should().HaveCountGreaterOrEqualTo(12); // have an unknown number of "Waiting for app started handling requests"
            logs.Should()
                .OnlyContain(x => x.Service == "LogsInjection.ILogger")
                .And.OnlyContain(x => x.Host == hostName)
                .And.OnlyContain(x => x.Source == "csharp")
                .And.OnlyContain(x => x.Env == "integration_tests")
                .And.OnlyContain(x => x.Version == "1.0.0")
                .And.OnlyContain(x => x.Exception == null)
                .And.OnlyContain(x => x.LogLevel == DirectSubmissionLogLevel.Information);

            VerifyInstrumentation(processResult.Process);
        }
    }
}
