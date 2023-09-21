// <copyright file="TimelineTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Timeline
{
    public class TimelineTest
    {
        private readonly ITestOutputHelper _output;

        public TimelineTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.BuggyBits")]
        public void CheckTimestampAsLabel(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 5");
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.AllocationProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.ContentionProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.LiveHeapProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.TimestampsAsLabelEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.GarbageCollectionProfilerEnabled, "1");
            // TODO: add any new profiler to ensure that all are setting timestamps as label

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);
            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);
            Assert.True(CheckTimestampsAsLabelForAllSamples(runner.Environment.PprofDir));
        }

        private bool CheckTimestampsAsLabelForAllSamples(string directory)
        {
            foreach (var profile in SamplesHelper.GetProfiles(directory))
            {
                foreach (var sample in profile.Sample)
                {
                    if (!sample.Labels(profile).Any(l => l.Name == "end_timestamp_ns"))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
