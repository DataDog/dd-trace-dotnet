// <copyright file="SerilogTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class SerilogTests : LogsInjectionTestBase
    {
        private readonly LogFileTest[] _log200FileTests =
            {
                new LogFileTest()
                {
                    FileName = "log-textFile.log",
                    RegexFormat = @"{0}: {1}",
                    UnTracedLogTypes = UnTracedLogTypes.EmptyProperties,
                    PropertiesUseSerilogNaming = true
                },
                new LogFileTest()
                {
                    FileName = "log-jsonFile.log",
                    RegexFormat = @"""{0}"":{1}",
                    UnTracedLogTypes = UnTracedLogTypes.None,
                    PropertiesUseSerilogNaming = true
                }
            };

        private readonly LogFileTest[] _logPre200FileTests =
            {
                new LogFileTest()
                {
                    FileName = "log-textFile.log",
                    RegexFormat = @"{0}: {1}",
                    TracedLogTypes = TracedLogTypes.NotCorrelated,
                    UnTracedLogTypes = UnTracedLogTypes.EmptyProperties,
                    PropertiesUseSerilogNaming = true
                }
            };

        public SerilogTests(ITestOutputHelper output)
            : base(output, "LogsInjection.Serilog")
        {
            SetServiceVersion("1.0.0");
        }

        [Theory]
        [MemberData(nameof(PackageVersions.Serilog), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "LinuxUnsupported")]
        public void InjectsLogsWhenEnabled(string packageVersion)
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");

            int agentPort = TcpPortProvider.GetOpenPort();
            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode} and exception: {processResult.StandardError}");

                var spans = agent.WaitForSpans(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

                if (string.IsNullOrWhiteSpace(packageVersion) || new Version(packageVersion) >= new Version("2.0.0"))
                {
                    ValidateLogCorrelation(spans, _log200FileTests);
                }
                else
                {
                    // We do not expect logs injection for Serilog versions < 2.0.0 so filter out all logs
                    ValidateLogCorrelation(spans, _logPre200FileTests);
                }
            }
        }

        [Theory]
        [MemberData(nameof(PackageVersions.Serilog), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "LinuxUnsupported")]
        public void DoesNotInjectLogsWhenDisabled(string packageVersion)
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "false");

            int agentPort = TcpPortProvider.GetOpenPort();
            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode} and exception: {processResult.StandardError}");

                var spans = agent.WaitForSpans(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

                if (string.IsNullOrWhiteSpace(packageVersion) || new Version(packageVersion) >= new Version("2.0.0"))
                {
                    ValidateLogCorrelation(spans, _log200FileTests, disableLogCorrelation: true);
                }
                else
                {
                    // We do not expect logs injection for Serilog versions < 2.0.0 so filter out all logs
                    ValidateLogCorrelation(spans, _logPre200FileTests, disableLogCorrelation: true);
                }
            }
        }
    }
}
