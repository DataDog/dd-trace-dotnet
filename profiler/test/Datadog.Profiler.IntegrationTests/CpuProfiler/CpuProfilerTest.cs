// <copyright file="CpuProfilerTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.IO;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.SmokeTests;
using Perftools.Profiles.Tests;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.CpuProfiler
{
    public class CpuProfilerTest
    {
        private const string CmdLine = "--timeout 10"; // default scenario is PI computation to run for 10 seconds
        private const int CpuValueSlot = 1;  // defined in enum class SampleValue (Sample.h)

        private readonly ITestOutputHelper _output;
        public CpuProfilerTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01", DisplayName = "Computer01")]
        public void NoCpuSampleIfCpuProfilerIsNotActivatedByDefault(string appName, string framework, string appAssembly)
        {
            using var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: CmdLine);
            CheckCpuProfiles(runner, false);
        }

        [TestAppFact("Samples.Computer01", DisplayName = "Computer01")]
        public void NoCpuSampleIfCpuProfilerIsDeactivated(string appName, string framework, string appAssembly)
        {
            using var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: CmdLine);
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "0");
            CheckCpuProfiles(runner, false);
        }

        [TestAppFact("Samples.Computer01", DisplayName = "Computer01")]
        public void GetCpuSamplesIfCpuProfilerIsActivated(string appName, string framework, string appAssembly)
        {
            using var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: CmdLine);
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");
            CheckCpuProfiles(runner, true);
        }

        private static int GetCpuSamplesCount(string file)
        {
            using var s = File.OpenRead(file);
            var profile = Profile.Parser.ParseFrom(s);
            var cpuSampleCount = 0;
            foreach (var sample in profile.Sample)
            {
                if (sample.Value[CpuValueSlot] > 0)
                {
                    cpuSampleCount++;
                }
            }

            return cpuSampleCount;
        }

        private void CheckCpuProfiles(TestApplicationRunner runner, bool isEnabled)
        {
            using var agent = new MockDatadogAgent(_output);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            int cpuSamplesCount = 0;
            foreach (var file in Directory.EnumerateFiles(runner.Environment.PprofDir, "*.pprof", SearchOption.AllDirectories))
            {
                cpuSamplesCount += GetCpuSamplesCount(file);
            }

            if (isEnabled)
            {
                Assert.True(cpuSamplesCount > 0);
            }
            else
            {
                Assert.Equal(0, cpuSamplesCount);
            }
        }
    }
}
