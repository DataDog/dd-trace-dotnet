// <copyright file="CpuAndWallTimeTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.IntegrationTests.Helpers.Profiles;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.CpuProfiler
{
    public class CpuAndWallTimeTest
    {
        private const string CmdLine = "--timeout 10"; // default scenario is PI computation to run for 10 seconds

        private readonly ITestOutputHelper _output;
        public CpuAndWallTimeTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void GetCpuSamplesIfCpuProfilerIsActivatedByDefault(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: CmdLine);
            runner.Environment.DisableAllExcept(ProfilerFeature.Cpu);

            using var run = runner.Run();

            run.Profiles.Should().HaveSampleTypes(ProfileValue.CpuTime, ProfileValue.CpuSampleCount);
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void GetWalltimeSamplesIfWalltimeProfilerIsActivatedByDefault(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: CmdLine);
            runner.Environment.DisableAllExcept(ProfilerFeature.WallTime);

            using var run = runner.Run();

            run.Profiles.Should().HaveSampleTypes(ProfileValue.WallTime);
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void NoSampleIfCpuAndWalltimeProfilersAreDeactivated(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: CmdLine);
            runner.Environment.EnableOnly();

            using var run = runner.Run();

            // no profiler enabled so should not see any sample
            Assert.Empty(run.Profiles.Samples);
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void GetCpuSamplesIfCpuProfilerIsActivated(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: CmdLine);
            runner.Environment.EnableOnly(ProfilerFeature.Cpu);

            using var run = runner.Run();

            run.Profiles.Should().HaveSampleTypes(ProfileValue.CpuTime, ProfileValue.CpuSampleCount);
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void CheckCpuDurationInSamples(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: CmdLine);
            runner.Environment.EnableOnly(ProfilerFeature.Cpu);

            using var run = runner.Run();

            // Ensure that we don't count too much CPU like when that nano/milli sec bug was introduced
            var cpuDuration = run.Profiles.Sum(ProfileValue.CpuTime);
            // Test is supposed to run 10s so count additional seconds both for extended duration + more than 1 managed thread (tracing code for example)
            // --> could be flacky otherwise
            var totalDuration = run.Duration.TotalMilliseconds * 1000000L;
            Assert.True(cpuDuration <= totalDuration);

            var cpuSamples = run.Profiles.Sum(ProfileValue.CpuSampleCount);
            cpuSamples.Should().BeGreaterThan(0);
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void CheckCpuDurationInSamplesForNewCpuProfiler(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: CmdLine);
            runner.Environment.EnableOnly(ProfilerFeature.Cpu);
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerType, "TimerCreate");

            using var run = runner.Run();

            CpuProfilerHelper.SkipIfTimerCreateWasDowngraded(run.LogDirectory);

            // Ensure that we don't count too much CPU like when that nano/milli sec bug was introduced
            var cpuDuration = run.Profiles.Sum(ProfileValue.CpuTime);
            // Test is supposed to run 10s so count additional seconds both for extended duration + more than 1 managed thread (tracing code for example)
            // --> could be flacky otherwise
            var totalDuration = run.Duration.TotalMilliseconds * 1000000L;
            Assert.True(cpuDuration <= totalDuration);
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void CheckWalltimeDurationInSamples(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: CmdLine);
            runner.Environment.EnableOnly(ProfilerFeature.WallTime);

            using var run = runner.Run();

            // the wall time duration depends on the number of managed threads
            // --> could be up to the number of threads x total test duration
            var walltimeDuration = run.Profiles.Sum(ProfileValue.WallTime);
            var managedThreadsCount = run.Profiles.Samples.DistinctLabelValues(ProfileLabelNames.ThreadId).Count;
            var totalDuration = (run.Duration.TotalMilliseconds * 1000000L) * managedThreadsCount;
            Assert.True(walltimeDuration <= totalDuration);
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void GetWalltimeSamplesIfWalltimeProfilerIsActivated(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: CmdLine);
            runner.Environment.EnableOnly(ProfilerFeature.WallTime);

            using var run = runner.Run();

            run.Profiles.Should().HaveSampleTypes(ProfileValue.WallTime);
        }
    }
}
