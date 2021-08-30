// <copyright file="ILoggerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NET452
using System;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
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
            SetCallTargetSettings(true);
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void InjectsLogs()
        {
            int agentPort = TcpPortProvider.GetOpenPort();
            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agent.Port, aspNetCorePort: 0))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode} and exception: {processResult.StandardError}");

                var spans = agent.WaitForSpans(1, 2500);
                spans.Should().HaveCountGreaterOrEqualTo(1);

                ValidateLogCorrelation(spans, _logFiles);
            }
        }
    }
}
#endif
