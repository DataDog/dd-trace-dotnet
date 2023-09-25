// <copyright file="LogsInjectionTestBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        protected static readonly string ExcludeMessagePrefix = "[ExcludeMessage]";

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

        public enum TracedLogTypes
        {
            /// <summary>
            /// Traced logs that include all dd_ properties
            /// </summary>
            Correlated,

            /// <summary>
            /// Traced logs that do not include any dd_ properties
            /// </summary>
            NotCorrelated
        }

        public enum UnTracedLogTypes
        {
            /// <summary>
            /// UnTraced logs do not include any dd_ properties
            /// </summary>
            None,

            /// <summary>
            /// UnTraced logs include the dd_ properties, but without any values
            /// </summary>
            EmptyProperties,

            /// <summary>
            /// UnTraced logs include dd_service, dd_env, and dd_version with their correct values
            /// but no dd_trace_id
            /// </summary>
            EnvServiceTracingPropertiesOnly,
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

        public void ValidateLogCorrelation(
            IReadOnlyCollection<MockSpan> spans,
            IEnumerable<LogFileTest> logFileTestCases,
            int expectedCorrelatedTraceCount,
            int expectedCorrelatedSpanCount,
            string packageVersion = "",
            bool disableLogCorrelation = false,
            Func<string, bool> additionalInjectedLogFilter = null)
        {
            foreach (var test in logFileTestCases)
            {
                // If we're testing a scenario without log correlation, disable the test case expectation of traced logs
                if (disableLogCorrelation)
                {
                    test.TracedLogTypes = TracedLogTypes.NotCorrelated;
                }

                var logFilePath = Path.Combine(EnvironmentHelper.GetSampleApplicationOutputDirectory(packageVersion), test.FileName);

                Output.WriteLine($"Loading logs from {logFilePath}");
                var logs = GetLogFileContents(logFilePath);
                logs.Should().NotBeNullOrEmpty();

                using var s = new AssertionScope(test.FileName);

                // Assumes we _only_ have logs for logs within traces + our startup log
                additionalInjectedLogFilter ??= (_) => true;
                var tracedLogs = logs.Where(log => !log.Contains(ExcludeMessagePrefix)).Where(additionalInjectedLogFilter).ToList();

                // Ensure that all spans are represented (when correlated) or no spans are represented (when not correlated) in the traced logs
                if (tracedLogs.Any())
                {
                    var traceIds = spans.Select(x => x.TraceId.ToString()).Distinct().ToList();
                    if (traceIds.Any())
                    {
                        switch (test.TracedLogTypes)
                        {
                            case TracedLogTypes.Correlated:
                                string.Join(",", tracedLogs).Should().ContainAll(traceIds);
                                break;
                            case TracedLogTypes.NotCorrelated:
                                string.Join(",", tracedLogs).Should().NotContainAny(traceIds);
                                break;
                            default:
                                throw new InvalidOperationException("Unknown TracedLogType: " + test.TracedLogTypes);
                        }
                    }
                }

                var versionProperty = test.PropertiesUseSerilogNaming ? "dd_version" : @"dd\.version";
                var envProperty = test.PropertiesUseSerilogNaming ? "dd_env" : @"dd\.env";
                var serviceProperty = test.PropertiesUseSerilogNaming ? "dd_service" : @"dd\.service";
                var traceIdProperty = test.PropertiesUseSerilogNaming ? "dd_trace_id" : @"dd\.trace_id";
                var spanIdProperty = test.PropertiesUseSerilogNaming ? "dd_span_id" : @"dd\.span_id";

                var versionRegex = string.Format(test.RegexFormat, versionProperty, @"""1.0.0""");
                var envRegex = string.Format(test.RegexFormat, envProperty, @"""integration_tests""");
                var serviceRegex = string.Format(test.RegexFormat, serviceProperty, @$"""{EnvironmentHelper.FullSampleName}""");
                var traceIdRegex = string.Format(test.RegexFormat, traceIdProperty, @"("")?(\d\d+)(?(1)\1|)"); // Match a string of digits or string of digits surrounded by double quotes. See https://stackoverflow.com/a/3569031
                var spanIdRegex = string.Format(test.RegexFormat, spanIdProperty, @"("")?(\d\d+)(?(1)\1|)"); // Match a string of digits or string of digits surrounded by double quotes. See https://stackoverflow.com/a/3569031

                HashSet<string> traceIdSet = new();
                HashSet<string> spanIdSet = new();
                foreach (var log in tracedLogs)
                {
                    switch (test.TracedLogTypes)
                    {
                        case TracedLogTypes.Correlated:
                            log.Should()
                               .MatchRegex(versionRegex)
                               .And.MatchRegex(envRegex)
                               .And.MatchRegex(serviceRegex)
                               .And.MatchRegex(traceIdRegex)
                               .And.MatchRegex(spanIdRegex);

                            var logTraceId = Regex.Match(log, traceIdRegex).Groups[2].Value;
                            traceIdSet.Add(logTraceId);

                            var logSpanId = Regex.Match(log, spanIdRegex).Groups[2].Value;
                            spanIdSet.Add(logSpanId);
                            break;
                        case TracedLogTypes.NotCorrelated:
                            log.Should()
                               .NotMatchRegex(versionRegex)
                               .And.NotMatchRegex(envRegex)
                               .And.NotMatchRegex(serviceRegex)
                               .And.NotMatchRegex(traceIdRegex)
                               .And.NotMatchRegex(spanIdRegex);
                            break;
                        default:
                            throw new InvalidOperationException("Unknown TracedLogType: " + test.TracedLogTypes);
                    }
                }

                traceIdSet.Should().HaveCount(expectedCorrelatedTraceCount);
                spanIdSet.Should().HaveCount(expectedCorrelatedSpanCount);

                // If logs are correlated, expect all SpanIDs in the traced logs to be represented in span list
                if (test.TracedLogTypes == TracedLogTypes.Correlated)
                {
                    var spanIdsInLogs = tracedLogs.Select(log => Regex.Match(log, spanIdRegex).Groups[2].Value);
                    var spanIds = spans.Select(x => x.SpanId.ToString()).Distinct().ToList();
                    if (spanIdsInLogs.Any())
                    {
                        spanIds.Should().Contain(spanIdsInLogs);
                    }
                }

                var unTracedLogs = logs.Where(log => log.Contains(ExcludeMessagePrefix)).ToList();

                foreach (var log in unTracedLogs)
                {
                    switch (test.UnTracedLogTypes)
                    {
                        case UnTracedLogTypes.None:
                            log.Should()
                               .NotMatchRegex(versionProperty)
                               .And.NotMatchRegex(envProperty)
                               .And.NotMatchRegex(serviceProperty)
                               .And.NotMatchRegex(traceIdProperty)
                               .And.NotMatchRegex(spanIdProperty);
                            break;
                        case UnTracedLogTypes.EmptyProperties:
                            log.Should()
                               .MatchRegex(versionProperty)
                               .And.MatchRegex(envProperty)
                               .And.MatchRegex(serviceProperty)
                               .And.MatchRegex(traceIdProperty)
                               .And.MatchRegex(spanIdProperty);
                            break;
                        case UnTracedLogTypes.EnvServiceTracingPropertiesOnly:
                            log.Should().MatchRegex(versionRegex);
                            log.Should().MatchRegex(envRegex);
                            log.Should().MatchRegex(serviceRegex);
                            log.Should().NotMatchRegex(traceIdProperty);
                            log.Should().NotMatchRegex(spanIdProperty);
                            break;
                        default:
                            throw new InvalidOperationException("Unknown UnTracedLogType: " + test.UnTracedLogTypes);
                    }
                }
            }
        }

        public class LogFileTest
        {
            public string FileName { get; set; }

            public string RegexFormat { get; set; }

            public TracedLogTypes TracedLogTypes { get; set; }

            public UnTracedLogTypes UnTracedLogTypes { get; set; }

            public bool PropertiesUseSerilogNaming { get; set; }
        }
    }
}
