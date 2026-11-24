// <copyright file="SigPendingLimitTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.IntegrationTests.Helpers.Logs;
using Datadog.Profiler.IntegrationTests.Xunit;
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

        [Flaky("Test flaked in CI")]
        [TestAppFact(TestApplication.Computer01)]
        public void CheckTimerCreateIsDowngradedWhenSignalQueueIsTooSmall(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: CmdLine);
            runner.Environment.EnableOnly(ProfilerFeature.Cpu);

            // asking for timer_create explicitly: the check must override the request, not only the default
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerType, "TimerCreate");

            using var run = runner.Run();

            run.Logs.NativeProfiler.Should()
                .ContainLine("Falling back to the manual CPU profiler")
                .And.ContainLine("Manual Cpu profiler is enabled")
                .And.NotContainLine("timer_create Cpu profiler is enabled");

            // the whole point of the fallback: CPU profiling keeps working
            run.Profiles.Samples.Should().NotBeEmpty("No samples were found");
        }
    }
}
