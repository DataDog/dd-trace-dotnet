// <copyright file="LogsInjectionTestBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public abstract class LogsInjectionTestBase : TestHelper
    {
        private readonly string _excludeMessagePrefix = "[ExcludeMessage]";

        public LogsInjectionTestBase(ITestOutputHelper output, string sampleName)
            : base(
                new EnvironmentHelper(
                    sampleName: sampleName,
                    typeof(TestHelper),
                    output,
                    prependSamplesToAppName: false),
                output)
        {
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

        public void ValidateLogCorrelation(IEnumerable<MockTracerAgent.Span> spans, IEnumerable<LogFileTest> logFileTestCases)
        {
            foreach (var test in logFileTestCases)
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
                    if (test.PropertiesUseSerilogNaming)
                    {
                        log.Should().MatchRegex(string.Format(test.RegexFormat, "dd_version", "1.0.0"));
                        log.Should().MatchRegex(string.Format(test.RegexFormat, "dd_env", "integration_tests"));
                        log.Should().MatchRegex(string.Format(test.RegexFormat, "dd_service", EnvironmentHelper.FullSampleName));
                        log.Should().NotMatchRegex(string.Format(test.RegexFormat, "dd_trace_id", "0"));
                    }
                    else
                    {
                        log.Should().MatchRegex(string.Format(test.RegexFormat, @"dd\.version", "1.0.0"));
                        log.Should().MatchRegex(string.Format(test.RegexFormat, @"dd\.env", "integration_tests"));
                        log.Should().MatchRegex(string.Format(test.RegexFormat, @"dd\.service", EnvironmentHelper.FullSampleName));
                        log.Should().NotMatchRegex(string.Format(test.RegexFormat, @"dd\.trace_id", "0"));
                    }
                }

                if (!test.PropertiesAreAlwaysPresent)
                {
                    var unTracedLogs = logs.Where(log => log.Contains(_excludeMessagePrefix)).ToList();

                    foreach (var log in unTracedLogs)
                    {
                        if (test.PropertiesUseSerilogNaming)
                        {
                            log.Should()
                               .NotMatchRegex("dd_version")
                               .And.NotMatchRegex("dd_env")
                               .And.NotMatchRegex("dd_service")
                               .And.NotMatchRegex("dd_trace_id");
                        }
                        else
                        {
                            log.Should()
                               .NotMatchRegex(@"dd\.version")
                               .And.NotMatchRegex(@"dd\.env")
                               .And.NotMatchRegex(@"dd\.service")
                               .And.NotMatchRegex(@"dd\.trace_id");
                        }
                    }
                }
            }
        }

        public class LogFileTest
        {
            public string FileName { get; set; }

            public string RegexFormat { get; set; }

            public bool PropertiesAreAlwaysPresent { get; set; }

            public bool PropertiesUseSerilogNaming { get; set; }
        }
    }
}
