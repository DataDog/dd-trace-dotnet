// <copyright file="TimerCreateCpuProfilerTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

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
        public void CheckCpuSamples(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: CmdLine);
            var samplingInterval = "21"; // ms
            // disable default profilers except CPU
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerType, "TimerCreate");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilingInterval, samplingInterval);

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            // only cpu  profiler enabled so should see 1 value per sample and
            foreach (var (_, _, values) in SamplesHelper.GetSamples(runner.Environment.PprofDir))
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

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            var expectedInterval = long.Parse(samplingInterval) * 1_000_000;
            // only cpu  profiler enabled so should see 1 value per sample and
            foreach (var (_, _, values) in SamplesHelper.GetSamples(runner.Environment.PprofDir))
            {
                values.Length.Should().Be(1);
                values.Should().OnlyContain(x => x == expectedInterval);
            }
        }
    }
}
