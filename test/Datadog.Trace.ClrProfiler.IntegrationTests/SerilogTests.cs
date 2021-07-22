// <copyright file="SerilogTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Datadog.Core.Tools;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class SerilogTests : LogsInjectionTestBase
    {
        private readonly LogFileTest[] _logFileTests =
            {
                new LogFileTest()
                {
                    FileName = "log-textFile.log",
                    RegexFormat = @"{0}: ""{1}""",
                    PropertiesAreAlwaysPresent = true,
                    PropertiesUseSerilogNaming = true
                },
                new LogFileTest()
                {
                    FileName = "log-jsonFile.log",
                    RegexFormat = @"""{0}"":""{1}""",
                    PropertiesAreAlwaysPresent = false,
                    PropertiesUseSerilogNaming = true
                }
            };

        public SerilogTests(ITestOutputHelper output)
            : base(output, "LogsInjection.CrossAppDomainCalls.Serilog")
        {
            SetServiceVersion("1.0.0");
        }

        [Theory]
        [MemberData(nameof(PackageVersions.Serilog), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "LinuxUnsupported")]
        public void InjectsLogs(string packageVersion)
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");

            int agentPort = TcpPortProvider.GetOpenPort();
            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode} and exception: {processResult.StandardError}");

                var spans = agent.WaitForSpans(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

                ValidateLogCorrelation(spans, _logFileTests);
            }
        }
    }
}
#endif
