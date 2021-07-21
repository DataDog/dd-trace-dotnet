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
    public class SerilogTests : TestHelper
    {
        private readonly string _excludeMessagePrefix = "[ExcludeMessage]";
        private readonly LogFileTest[] _logFileTests =
            {
                new LogFileTest()
                {
                    FileName = "log-textFile.log",
                    RegexFormat = @"{0}: ""{1}""",
                    PropertiesAlwaysPresent = true
                },
                new LogFileTest()
                {
                    FileName = "log-jsonFile.log",
                    RegexFormat = @"""{0}"":""{1}""",
                    PropertiesAlwaysPresent = false
                }
            };

        public SerilogTests(ITestOutputHelper output)
            : base(
                new EnvironmentHelper(
                    sampleName: "LogsInjection.CrossAppDomainCalls.Serilog",
                    typeof(TestHelper),
                    output,
                    prependSamplesToAppName: false),
                output)
        {
            SetServiceVersion("1.0.0");
        }

        [Theory]
        [MemberData(nameof(PackageVersions.Serilog), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
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

                foreach (var test in _logFileTests)
                {
                    var logFilePath = Path.Combine(EnvironmentHelper.GetSampleApplicationOutputDirectory(), test.FileName);
                    var logs = GetLogFileContents(logFilePath);
                    logs.Should().NotBeNullOrEmpty();

                    using var s = new AssertionScope(test.FileName);

                    // Assumes we _only_ have logs for logs within traces + our startup log
                    var tracedLogs = logs.Where(log => !log.Contains(_excludeMessagePrefix)).ToList();

                    // all spans should be represented in the traced logs
                    var traceIds = spans.Select(x => x.TraceId.ToString()).Distinct().ToList();
                    if (traceIds.Any())
                    {
                        string.Join(",", tracedLogs).Should().ContainAll(traceIds);
                    }

                    foreach (var log in tracedLogs)
                    {
                        log.Should().MatchRegex(string.Format(test.RegexFormat, "dd_version", "1.0.0"));
                        log.Should().MatchRegex(string.Format(test.RegexFormat, "dd_env", "integration_tests"));
                        log.Should().MatchRegex(string.Format(test.RegexFormat, "dd_service", EnvironmentHelper.FullSampleName));
                        log.Should().NotMatchRegex(string.Format(test.RegexFormat, "dd_trace_id", "0"));
                    }

                    if (!test.PropertiesAlwaysPresent)
                    {
                        var unTracedLogs = logs.Where(log => log.Contains(_excludeMessagePrefix)).ToList();

                        foreach (var log in unTracedLogs)
                        {
                            log.Should()
                               .NotMatchRegex("dd_version")
                               .And.NotMatchRegex("dd_env")
                               .And.NotMatchRegex("dd_service")
                               .And.NotMatchRegex("dd_trace_id");
                        }
                    }
                }
            }
        }

        public string[] GetLogFileContents(string logFile)
        {
            File.Exists(logFile).Should().BeTrue($"'{logFile}' should exist");

            // may have a lingering lock, so retry
            var retryCount = 5;
            var millisecondsToWait = 15_000 / retryCount;
            Exception ex = null;
            while (retryCount > 0)
            {
                try
                {
                    return File.ReadAllLines(logFile);
                }
                catch (Exception e)
                {
                    ex = e;
                    Thread.Sleep(millisecondsToWait);
                }

                retryCount--;
            }

            throw new Exception("Unable to Fetch Log File Contents", ex);
        }

        internal class LogFileTest
        {
            public string FileName { get; set; }

            public string RegexFormat { get; set; }

            public bool PropertiesAlwaysPresent { get; set; }
        }
    }
}
#endif
