// <copyright file="GarbageCollectionsProfilerTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.GarbageCollections
{
    public class GarbageCollectionsProfilerTest
    {
        private const string ScenarioGenerics = "--scenario 12";
        private const string GcRootFrame = "|lm: |ns: |ct: |cg: |fn:Garbage Collector |fg: |sg:";

        private readonly ITestOutputHelper _output;

        public GarbageCollectionsProfilerTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net7.0" })]
        public void ShouldGetGarbageCollectionSamples(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioGenerics);
            EnvironmentHelper.DisableDefaultProfilers(runner);

            // enable GC profiler
            runner.Environment.SetVariable(EnvironmentVariables.GarbageCollectionProfilerEnabled, "1");

            // only garbage collection profiler enabled so should only see the 1 related value per sample + Generation label
            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);
            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);
            Assert.True(CheckSamplesAreGC(runner.Environment.PprofDir));
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net7.0" })]
        public void ShouldGetGarbageCollectionSamplesByDefault(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioGenerics);

            // disable default profilers except GC
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.ContentionProfilerEnabled, "0");

            // only garbage collection profiler enabled so should only see the 1 related value per sample + Generation label
            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);
            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);
            Assert.True(CheckSamplesAreGC(runner.Environment.PprofDir));
        }

        private bool CheckSamplesAreGC(string directory)
        {
            var rootFrame = new StackFrame(GcRootFrame);

            int profileCount = 0;
            foreach (var profile in SamplesHelper.GetProfiles(directory))
            {
                int sampleCount = 0;
                foreach (var sample in profile.Sample)
                {
                    if (!sample.Labels(profile).Any(l => l.Name == "gc generation"))
                    {
                        return false;
                    }

                    // check fake call stack
                    var frames = sample.StackTrace(profile);
                    if (frames.FramesCount != 2)
                    {
                        return false;
                    }

                    if (!frames[0].Equals(rootFrame))
                    {
                        return false;
                    }

                    var generation = frames[1].Function;
                    if (!generation.StartsWith("gen"))
                    {
                        return false;
                    }

                    if (!(generation.EndsWith("0") || generation.EndsWith("1") || generation.EndsWith("2")))
                    {
                        return false;
                    }

                    sampleCount++;
                }

                if (sampleCount == 0)
                {
                    return false;
                }

                profileCount++;
            }

            return (profileCount > 0);
        }
    }
}
