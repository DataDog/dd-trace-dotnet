// <copyright file="ContentionProfilerTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.SmokeTests;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Contention
{
    public class ContentionProfilerTest
    {
        private const string ScenarioContention = "--scenario 10 --threads 20";

        private readonly ITestOutputHelper _output;

        public ContentionProfilerTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net7.0" })]
        public void ShouldGetContentionSamples(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioContention);
            // disable default profilers
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.GarbageCollectionProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.ContentionProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            // only contention profiler enabled so should only see the 2 related values per sample
            SamplesHelper.CheckSamplesValueCount(runner.Environment.PprofDir, 2);
            Assert.True(SamplesHelper.IsLabelPresent(runner.Environment.PprofDir, "raw duration"));
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net7.0" })]
        public void ShouldContentionProfilerBeDisabledByDefault(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioContention);
            // disable default profilers
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.GarbageCollectionProfilerEnabled, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            // no profiler enabled so should not see any sample
            Assert.Equal(0, SamplesHelper.GetSamplesCount(runner.Environment.PprofDir));
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net7.0" })]
        public void ExplicitlyDisableContentionProfiler(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioContention);

            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.GarbageCollectionProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.ContentionProfilerEnabled, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            // only walltime profiler enabled so should see 1 value per sample
            SamplesHelper.CheckSamplesValueCount(runner.Environment.PprofDir, 1);
        }
    }
}
