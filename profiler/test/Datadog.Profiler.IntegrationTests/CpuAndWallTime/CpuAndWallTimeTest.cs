// <copyright file="CpuAndWallTimeTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Datadog.Profiler.IntegrationTests.Helpers;
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

        [TestAppFact("Samples.Computer01")]
        public void GetCpuSamplesIfCpuProfilerIsActivatedByDefault(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: CmdLine);
            // disable default profilers except CPU
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.GarbageCollectionProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.ContentionProfilerEnabled, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            // only cpu  profiler enabled so should see 1 value per sample
            SamplesHelper.CheckSamplesValueCount(runner.Environment.PprofDir, 1);
        }

        [TestAppFact("Samples.Computer01")]
        public void GetWalltimeSamplesIfWalltimeProfilerIsActivatedByDefault(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: CmdLine);
            // disable default profilers except wall time
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.GarbageCollectionProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.ContentionProfilerEnabled, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            // only wall time profiler enabled so should see 1 value per sample
            SamplesHelper.CheckSamplesValueCount(runner.Environment.PprofDir, 1);
        }

        [TestAppFact("Samples.Computer01")]
        public void NoSampleIfCpuAndWalltimeProfilersAreDeactivated(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: CmdLine);
            EnvironmentHelper.DisableDefaultProfilers(runner);

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            // no profiler enabled so should not see any sample
            Assert.Equal(0, SamplesHelper.GetSamplesCount(runner.Environment.PprofDir));
        }

        [TestAppFact("Samples.Computer01")]
        public void GetCpuSamplesIfCpuProfilerIsActivated(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: CmdLine);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            // only cpu  profiler enabled so should see 1 value per sample
            SamplesHelper.CheckSamplesValueCount(runner.Environment.PprofDir, 1);
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckCpuDurationInSamples(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: CmdLine);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            // Ensure that we don't count too much CPU like when that nano/milli sec bug was introduced
            var cpuDuration = SamplesHelper.GetValueSum(runner.Environment.PprofDir, 0);
            // Test is supposed to run 10s so count additional seconds both for extended duration + more than 1 managed thread (tracing code for example)
            // --> could be flacky otherwise
            var totalDuration = runner.TotalTestDurationInMilliseconds * 1000000L;
            Assert.True(cpuDuration <= totalDuration);
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckWalltimeDurationInSamples(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: CmdLine);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            // the wall time duration depends on the number of managed threads
            // --> could be up to the number of threads x total test duration
            var walltimeDuration = SamplesHelper.GetValueSum(runner.Environment.PprofDir, 0);
            var managedThreadsCount = SamplesHelper.GetThreadCount(runner.Environment.PprofDir);
            var totalDuration = (runner.TotalTestDurationInMilliseconds * 1000000L) * managedThreadsCount;
            Assert.True(walltimeDuration <= totalDuration);
        }

        [TestAppFact("Samples.Computer01")]
        public void GetWalltimeSamplesIfWalltimeProfilerIsActivated(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: CmdLine);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            // only wall time profiler enabled so should see 1 value per sample
            SamplesHelper.CheckSamplesValueCount(runner.Environment.PprofDir, 1);
        }
    }
}
