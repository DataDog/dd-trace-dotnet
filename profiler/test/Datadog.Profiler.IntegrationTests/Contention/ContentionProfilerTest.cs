// <copyright file="ContentionProfilerTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
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

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net7.0", "net8.0" })]
        public void ShouldGetContentionSamples(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioContention);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.ContentionProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            // only contention profiler enabled so should only see the 2 related values per sample
            SamplesHelper.CheckSamplesValueCount(runner.Environment.PprofDir, 2);
            Assert.True(SamplesHelper.IsLabelPresent(runner.Environment.PprofDir, "raw duration"));

            if (framework == "net8.0")
            {
                AssertBlockingThreadLabel(runner.Environment.PprofDir);
            }
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net7.0", "net8.0" })]
        public void ShouldContentionProfilerBeEnabledByDefault(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioContention);

            // disable default profilers except contention
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.GarbageCollectionProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            // only contention profiler enabled so should see 2 value per sample
            SamplesHelper.CheckSamplesValueCount(runner.Environment.PprofDir, 2);
            Assert.NotEqual(0, SamplesHelper.GetSamplesCount(runner.Environment.PprofDir));

            if (framework == "net8.0")
            {
                AssertBlockingThreadLabel(runner.Environment.PprofDir);
            }
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net7.0", "net8.0" })]
        public void ExplicitlyDisableContentionProfiler(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioContention);

            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            // only walltime profiler enabled so should see 1 value per sample
            SamplesHelper.CheckSamplesValueCount(runner.Environment.PprofDir, 1);
        }

        private static void AssertBlockingThreadLabel(string pprofDir)
        {
            var threadIds = SamplesHelper.GetThreadIds(pprofDir);
            // get samples with lock-count value set and blocking thread info
            var contentionSamples = SamplesHelper.GetSamples(pprofDir, "lock-count")
                .Where(e => e.Labels.Any(x => x.Name == "blocking thread"));

            contentionSamples.Should().NotBeEmpty();

            foreach (var (_, labels, _) in contentionSamples)
            {
                var label = labels.FirstOrDefault(l => l.Name == "blocking thread");
                label.Name.Should().NotBeNullOrWhiteSpace();
                threadIds.Should().Contain(int.Parse(label.Value), $"Unknown blocking thread id {label.Value}");
            }
        }
    }
}
