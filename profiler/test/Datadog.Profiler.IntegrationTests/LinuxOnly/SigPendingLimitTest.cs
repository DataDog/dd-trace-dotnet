// <copyright file="SigPendingLimitTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.IO;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.LinuxOnly
{
    /// <summary>
    /// Checks that the profiler stops asking for timer_create when the signal queue is too small for
    /// it. Every timer_create timer holds a signal queue slot, so the run this test belongs to is
    /// started with a container-wide RLIMIT_SIGPENDING far below what the profiler asks for.
    /// </summary>
    [Trait("Category", "LinuxOnly")]
    [Trait("Category", "SigPendingLimitTest")]
    public class SigPendingLimitTest
    {
        private const string CmdLine = "--timeout 10"; // default scenario is PI computation to run for 10 seconds

        private readonly ITestOutputHelper _output;

        public SigPendingLimitTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckTimerCreateIsDowngradedWhenSignalQueueIsTooSmall(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: CmdLine);
            // disable default profilers except CPU
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");

            // asking for timer_create explicitly: the check must override the request, not only the default
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerType, "TimerCreate");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
               .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            var logLines = File.ReadLines(logFile);

            logLines.Should().ContainMatch("*Falling back to the manual CPU profiler*");
            logLines.Should().ContainMatch("*Manual Cpu profiler is enabled*");
            logLines.Should().NotContainMatch("*timer_create Cpu profiler is enabled*");

            // the whole point of the fallback: CPU profiling keeps working
            SamplesHelper.GetSamples(runner.Environment.PprofDir).Should().NotBeEmpty("No samples were found");
        }
    }
}
