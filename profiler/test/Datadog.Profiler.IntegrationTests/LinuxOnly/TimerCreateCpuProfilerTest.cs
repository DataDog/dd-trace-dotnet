// <copyright file="TimerCreateCpuProfilerTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.IntegrationTests.Helpers.Logs;
using Datadog.Profiler.IntegrationTests.Helpers.Profiles;
using Datadog.Profiler.IntegrationTests.Xunit;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.LinuxOnly
{
    [Trait("Category", "LinuxOnly")]
    public class TimerCreateCpuProfilerTest
    {
        private const string CmdLine = "--timeout 10"; // default scenario is PI computation to run for 10 seconds

        private readonly ITestOutputHelper _output;

        public TimerCreateCpuProfilerTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact(TestApplication.Computer01)]
        public void CheckTimerCreateIsDefault(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: CmdLine);
            runner.Environment.EnableOnly(ProfilerFeature.Cpu);

            using var run = runner.Run();

            CpuProfilerHelper.SkipIfTimerCreateWasDowngraded(run.LogDirectory);

            run.Logs.NativeProfiler.Should()
                .ContainLine("timer_create Cpu profiler is enabled")
                .And.NotContainLine("Manual Cpu profiler is enabled");

            run.Profiles.Samples.Should().NotBeEmpty("No samples were found");
        }

        [Flaky("Manual CPU profiling intermittently produces no samples in CI")]
        [TestAppFact(TestApplication.Computer01)]
        public void CheckTimerCreateIsDisabledWhenManualIsSet(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: CmdLine);
            runner.Environment.EnableOnly(ProfilerFeature.Cpu);
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerType, "ManualCpuTime");

            using var run = runner.Run();

            run.Logs.NativeProfiler.Should()
                .NotContainLine("timer_create Cpu profiler is enabled")
                .And.ContainLine("Manual Cpu profiler is enabled");

            run.Profiles.Samples.Should().NotBeEmpty("No samples were found");
        }

        [TestAppFact(TestApplication.Computer01)]
        public void CheckLogForError(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: CmdLine);
            runner.Environment.EnableOnly(ProfilerFeature.Cpu);
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerType, "TimerCreate");

            using var run = runner.Run();

            CpuProfilerHelper.SkipIfTimerCreateWasDowngraded(run.LogDirectory);

            run.Logs.NativeProfiler.Should()
                .ContainLine("timer_create Cpu profiler is enabled")
                .And.NotContainLine("Call to timer_create failed for thread 0")
                .And.NotContainLine("Timer was already created for thread 0");
            run.Profiles.Samples.Should().NotBeEmpty("No samples were found");
        }

        [Flaky("timer_create CPU profiling intermittently produces no samples in CI")]
        [TestAppFact(TestApplication.Computer01)]
        public void CheckCpuSamples(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: CmdLine);
            var samplingInterval = "21"; // ms
            runner.Environment.EnableOnly(ProfilerFeature.Cpu);
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerType, "TimerCreate");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilingInterval, samplingInterval);

            using var run = runner.Run();

            CpuProfilerHelper.SkipIfTimerCreateWasDowngraded(run.LogDirectory);

            // only cpu profiler enabled so should see 2 values per sample
            run.Profiles.Samples.Should().NotBeEmpty();
            run.Profiles.Should().HaveSampleTypes(ProfileValue.CpuTime, ProfileValue.CpuSampleCount);
            foreach (var sample in run.Profiles.Samples)
            {
                sample.Values.GetRequired(ProfileValue.CpuTime).Should().Be(long.Parse(samplingInterval) * 1_000_000);
                sample.Values.GetRequired(ProfileValue.CpuSampleCount).Should().BeGreaterThan(0);
            }

            AssertTimestampInProfile(run.Profiles, runner.ProfilingExportsIntervalInSeconds);
        }

        [TestAppFact(TestApplication.Computer01)]
        public void CheckCpuSamplesForDefaultSampingInterval(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: CmdLine);
            var samplingInterval = "9"; // ms (default)
            runner.Environment.EnableOnly(ProfilerFeature.Cpu);
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerType, "TimerCreate");

            using var run = runner.Run();

            CpuProfilerHelper.SkipIfTimerCreateWasDowngraded(run.LogDirectory);

            var expectedInterval = long.Parse(samplingInterval) * 1_000_000;
            // only cpu profiler enabled so should see 2 values per sample
            run.Profiles.Samples.Should().NotBeEmpty();
            run.Profiles.Should().HaveSampleTypes(ProfileValue.CpuTime, ProfileValue.CpuSampleCount);
            foreach (var sample in run.Profiles.Samples)
            {
                sample.Values.GetRequired(ProfileValue.CpuTime).Should().Be(expectedInterval);
                sample.Values.GetRequired(ProfileValue.CpuSampleCount).Should().BeGreaterThan(0);
            }

            AssertTimestampInProfile(run.Profiles, runner.ProfilingExportsIntervalInSeconds);
        }

        private static void AssertTimestampInProfile(ProfileOutput profiles, int exportIntervalSec)
        {
            foreach (var profile in profiles.Profiles)
            {
                var start = profile.TimeNanos;
                var end = profile.DurationNanos + start;
                var previousCollectionStart = start - (exportIntervalSec * 1_000_000_000_000);
                foreach (var sample in profile.Samples)
                {
                    var endTimestamp = long.Parse(sample.Labels.GetRequired("end_timestamp_ns"));

                    // we check that end_timestamp_ns is either part of the previous collection
                    // either the current collection.
                    endTimestamp.Should().BeInRange(previousCollectionStart, end);

                    // same for timeline
                    var timelineTimestamp = sample.Values.TryGetValue(ProfileValue.Timeline);
                    if (timelineTimestamp.IsPresent)
                    {
                        timelineTimestamp.Value.Should().BeInRange(previousCollectionStart, end);
                    }
                }
            }
        }
    }
}
