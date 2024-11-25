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
                values.Length.Should().Be(1);
                values.Should().OnlyContain(x => x == long.Parse(samplingInterval) * 1_000_000);
            }
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
            // only cpu  profiler enabled so should see 1 value per sample and
            var samples = SamplesHelper.GetSamples(runner.Environment.PprofDir);
            samples.Should().NotBeEmpty();
            foreach (var (_, _, values) in samples)
            {
                values.Length.Should().Be(1);
                values.Should().OnlyContain(x => x == expectedInterval);
            }
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckDefaultCpuSamplingInterval(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: CmdLine);
            var samplingInterval = "9"; // ms (default)
            // disable default profilers except CPU
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerType, "TimerCreate");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            var expectedInterval = long.Parse(samplingInterval) * 1_000_000;
            // only cpu  profiler enabled so should see 1 value per sample and
            var samples = SamplesHelper.GetSamples(runner.Environment.PprofDir);
            samples.Should().NotBeEmpty();
            foreach (var (_, _, values) in samples)
            {
                values.Length.Should().Be(1);
                values.Should().OnlyContain(x => x == expectedInterval);
            }
        }
    }
}
