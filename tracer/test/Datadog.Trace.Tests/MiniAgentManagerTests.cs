// <copyright file="MiniAgentManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests
{
    public class MiniAgentManagerTests
    {
        private readonly ITestOutputHelper output;

        public MiniAgentManagerTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Theory]
        [InlineData("[2023-06-06T01:31:30Z DEBUG datadog_trace_mini_agent::mini_agent] Random Log", "DEBUG", "Random Log")]
        [InlineData("[2023-06-06T01:31:30Z ERROR datadog_trace_mini_agent::mini_agent] Random Log", "ERROR", "Random Log")]
        [InlineData("[2023-06-06T01:31:30Z WARN datadog_trace_mini_agent::mini_agent] Random Log", "WARN", "Random Log")]
        [InlineData("[2023-06-06T01:31:30Z INFO datadog_trace_mini_agent::mini_agent] Random Log", "INFO", "Random Log")]
        [InlineData("[2023-06-06T01:31:30Z YELL datadog_trace_mini_agent::mini_agent] Random Log", "INFO", "[2023-06-06T01:31:30Z YELL datadog_trace_mini_agent::mini_agent] Random Log")]
        [InlineData("Random Log", "INFO", "Random Log")]
        internal void CleanAndProperlyLogMiniAgentLogs(string rawLog, string expectedLevel, string expectedLog)
        {
            var logTuple = MiniAgentManager.ProcessMiniAgentLog(rawLog);
            string level = logTuple.Item1;
            string processedLog = logTuple.Item2;
            output.WriteLine("we expected level " + expectedLevel + " and log " + expectedLog);
            output.WriteLine("we got level " + level + " and log " + processedLog);
            Assert.Equal(expectedLevel, level);
            Assert.Equal(expectedLog, processedLog);
        }
    }
}
