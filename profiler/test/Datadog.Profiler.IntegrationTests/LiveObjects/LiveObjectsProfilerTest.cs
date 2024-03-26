// <copyright file="LiveObjectsProfilerTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.LiveObjects
{
    public class LiveObjectsProfilerTest
    {
        private const string ScenarioGenerics = "--scenario 13";

        private readonly ITestOutputHelper _output;

        public LiveObjectsProfilerTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01", new[] { "net7.0" })]
        public void ShouldGetGarbageCollectionSamples(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioGenerics);

            EnvironmentHelper.DisableDefaultProfilers(runner);

            // enable Live Objects profiler
            runner.Environment.SetVariable(EnvironmentVariables.LiveHeapProfilerEnabled, "1");

            // only garbage collection profiler enabled so should only see the 1 related value per sample + Generation label
            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);
            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);
            Assert.True(CheckSamplesAreLiveObjects(runner.Environment.PprofDir));
        }

        private bool CheckSamplesAreLiveObjects(string directory)
        {
            int profileCount = 0;
            foreach (var profile in SamplesHelper.GetProfiles(directory))
            {
                int sampleCount = 0;
                foreach (var sample in profile.Sample)
                {
                    if (!sample.Labels(profile).Any(l => l.Name == "object lifetime"))
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
