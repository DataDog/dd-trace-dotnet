// <copyright file="TimerCreateCpuProfilerTest.cs" company="Datadog">
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
    [Trait("Category", "LinuxOnly")]
    public class TimerCreateCpuProfilerTest
    {
        private const string CmdLine = "--timeout 10"; // default scenario is PI computation to run for 10 seconds

        private readonly ITestOutputHelper _output;

        public TimerCreateCpuProfilerTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckTimerCreateIsDefault(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: CmdLine);
            // disable default profilers except CPU
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
               .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            var logLines = File.ReadLines(logFile);

            logLines.Should().ContainMatch("*timer_create Cpu profiler is enabled*");
            logLines.Should().NotContainMatch("*Manual Cpu profiler is enabled*");

            SamplesHelper.GetSamples(runner.Environment.PprofDir).Should().NotBeEmpty("No samples were found");
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckTimerCreateIsDisabledWhenManualIsSet(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: CmdLine);
            // disable default profilers except CPU
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerType, "ManualCpuTime");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
               .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            var logLines = File.ReadLines(logFile);

            logLines.Should().NotContainMatch("*timer_create Cpu profiler is enabled*");
            logLines.Should().ContainMatch("*Manual Cpu profiler is enabled*");

            SamplesHelper.GetSamples(runner.Environment.PprofDir).Should().NotBeEmpty("No samples were found");
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckLogForError(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: CmdLine);
            // disable default profilers except CPU
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerType, "TimerCreate");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
               .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            var logLines = File.ReadLines(logFile);

            logLines.Should().ContainMatch("*timer_create Cpu profiler is enabled*");

            logLines.Should().NotContainMatch("*Call to timer_create failed for thread 0*");
            logLines.Should().NotContainMatch("*Timer was already created for thread 0*");
            logLines.Should().NotContainMatch("*Call to timer_create failed for thread 0*");
            SamplesHelper.GetSamples(runner.Environment.PprofDir).Should().NotBeEmpty("No samples were found");
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckCpuSamples(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: CmdLine);
            var samplingInterval = "21"; // ms
            // disable default profilers except CPU
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerType, "TimerCreate");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilingInterval, samplingInterval);

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            // only cpu  profiler enabled so should see 1 value per sample and
            var samples = SamplesHelper.GetSamples(runner.Environment.PprofDir);
            samples.Should().NotBeEmpty();
            foreach (var (_, _, values) in samples)
            {
                values.Length.Should().Be(2);
                values[0].Should().Be(long.Parse(samplingInterval) * 1_000_000);
                values[1].Should().BeGreaterThan(0);
            }

            AssertTimestampInProfile(runner.Environment.PprofDir, runner.ProfilingExportsIntervalInSeconds);
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckCpuSamplesForDefaultSampingInterval(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: CmdLine);
            var samplingInterval = "9"; // ms (default)
            // disable default profilers except CPU
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerType, "TimerCreate");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            var expectedInterval = long.Parse(samplingInterval) * 1_000_000;
            // only cpu  profiler enabled so should see 2 value per sample and
            var samples = SamplesHelper.GetSamples(runner.Environment.PprofDir);
            samples.Should().NotBeEmpty();
            foreach (var (_, _, values) in samples)
            {
                values.Length.Should().Be(2);
                values[0].Should().Be(expectedInterval);
                values[1].Should().BeGreaterThan(0);
            }

            AssertTimestampInProfile(runner.Environment.PprofDir, runner.ProfilingExportsIntervalInSeconds);
        }

        private static void AssertTimestampInProfile(string pprofFolder, int exportIntervalSec)
        {
            foreach (var profile in SamplesHelper.GetProfiles(pprofFolder))
            {
                var start = profile.TimeNanos;
                var end = profile.DurationNanos + start;
                var previousCollectionStart = start - (exportIntervalSec * 1_000_000_000_000);
                foreach (var sample in profile.Sample)
                {
                    var samplex = Sample.Create(profile, sample);
                    var endTimestamp = long.Parse(samplex.Labels["end_timestamp_ns"]);

                    // we check that end_timestamp_ns is either part of the previous collection
                    // either the current collection.
                    endTimestamp.Should().BeInRange(previousCollectionStart, end);

                    // same for timeline
                    if (samplex.Values.TryGetValue("timeline", out var timelineTimestamp))
                    {
                        timelineTimestamp.Should().BeInRange(previousCollectionStart, end);
                    }
                }
            }
        }
    }
}
