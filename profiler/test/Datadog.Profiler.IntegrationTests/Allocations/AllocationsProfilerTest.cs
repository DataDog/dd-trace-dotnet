// <copyright file="AllocationsProfilerTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.SmokeTests;
using FluentAssertions;
using Perftools.Profiles;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Allocations
{
    public class AllocationsProfilerTest
    {
        private const int AllocationCountSlot = 3;  // defined in enum class SampleValue (Sample.h)
        private const int AllocationSizeSlot = 4;
        private const string ScenarioGenerics = "--scenario 9";

        private readonly ITestOutputHelper _output;

        public AllocationsProfilerTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0" })]
        public void ShouldGetAllocationSamples(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioGenerics);
            runner.Environment.SetVariable(EnvironmentVariables.AllocationProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "0");

            CheckExceptionProfiles(runner);
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0" })]
        public void ShouldAllocationProfilerBeDisabledByDefault(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioGenerics);
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "0");

            using var agent = new MockDatadogAgent(_output);

            runner.Run(agent);

            // On alpine, this check is flaky.
            // Disable it on alpine for now
            if (!EnvironmentHelper.IsAlpine)
            {
                Assert.True(agent.NbCallsOnProfilingEndpoint == 0);
            }

            ExtractAllocationSamples(runner.Environment.PprofDir).Should().BeEmpty();
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0" })]
        public void ExplicitlyDisableExceptionProfiler(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioGenerics);

            runner.Environment.SetVariable(EnvironmentVariables.AllocationProfilerEnabled, "0");

            using var agent = new MockDatadogAgent(_output);

            runner.Run(agent);

            ExtractAllocationSamples(runner.Environment.PprofDir).Should().BeEmpty();
        }

        private static IEnumerable<(string Type, long Count, long Size, StackTrace Stacktrace)> ExtractAllocationSamples(string directory)
        {
            static IEnumerable<(string Type, long Count, long Size, StackTrace Stacktrace, long Time)> GetAllocationSamples(string directory)
            {
                foreach (var file in Directory.EnumerateFiles(directory, "*.pprof", SearchOption.AllDirectories))
                {
                    using var stream = File.OpenRead(file);
                    var profile = Profile.Parser.ParseFrom(stream);

                    foreach (var sample in profile.Sample)
                    {
                        var count = sample.Value[AllocationCountSlot];
                        if (count == 0)
                        {
                            continue;
                        }
                        var size = sample.Value[AllocationSizeSlot];

                        var labels = sample.Labels(profile).ToArray();

                        // from Sample.cpp
                        var type = labels.Single(l => l.Name == "allocation class").Value;

                        yield return (type, count, size, sample.StackTrace(profile), profile.TimeNanos);
                    }
                }
            }

            return GetAllocationSamples(directory)
                .OrderBy(s => s.Time)
                .Select(s => (s.Type, s.Count, s.Size, s.Stacktrace));
        }

        private void CheckExceptionProfiles(TestApplicationRunner runner)
        {
            using var agent = new MockDatadogAgent(_output);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var allocationSamples = ExtractAllocationSamples(runner.Environment.PprofDir).ToArray();
            allocationSamples.Should().NotBeEmpty();

            bool arrayFound = false;
            bool elementFound = false;

            foreach (var sample in allocationSamples)
            {
                // still a bug for array of generic
                if (sample.Type.CompareTo("Samples.Computer01.Generic`1[System.Int32][]") == 0)
                {
                    arrayFound = true;
                }
                else
                if (sample.Type.CompareTo("Samples.Computer01.Generic<System.Int32>") == 0)
                {
                    elementFound = true;
                }
            }

            Assert.True(arrayFound);
            Assert.True(elementFound);
        }
    }
}
