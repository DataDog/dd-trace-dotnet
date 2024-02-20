// <copyright file="ThreadLifetimeProviderTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Threads
{
    public class ThreadLifetimeProviderTest
    {
        private const string ScenarioGenerics = "--scenario 14";
        private const string ThreadStartFrame = "|lm: |ns: |ct: |cg: |fn:Thread Start |fg: |sg:";
        private const string ThreadStopFrame = "|lm: |ns: |ct: |cg: |fn:Thread Stop |fg: |sg:";

        private readonly ITestOutputHelper _output;

        public ThreadLifetimeProviderTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01", new[] { /*"net462", "net48",*/ "net6.0", "net7.0" })]
        public void ShouldGetThreadLifetimeSamples(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioGenerics);

            EnvironmentHelper.DisableDefaultProfilers(runner);

            // enable thread lifetime provider
            runner.Environment.SetVariable(EnvironmentVariables.ThreadLifetimeEnabled, "1");

            // only thread lifetime profiler is enabled so should only see the corresponding samples
            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);
            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);
            Assert.True(CheckSamplesAreThreadTimeline(runner.Environment.PprofDir));
        }

        [TestAppFact("Samples.Computer01", new[] { /*"net462", "net48",*/ "net6.0", "net7.0" })]
        public void ShouldNotGetThreadLifetimeSamplesByDefault(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioGenerics);

            EnvironmentHelper.DisableDefaultProfilers(runner);

            // only thread lifetime profiler is enabled so should only see the corresponding samples
            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);
            Assert.False(agent.NbCallsOnProfilingEndpoint > 0);
            Assert.False(CheckSamplesAreThreadTimeline(runner.Environment.PprofDir));
        }

        private bool CheckSamplesAreThreadTimeline(string directory)
        {
            var threadStartFrame = new StackFrame(ThreadStartFrame);
            var threadStopFrame = new StackFrame(ThreadStopFrame);

            int profileCount = 0;
            foreach (var profile in SamplesHelper.GetProfiles(directory))
            {
                int sampleCount = 0;
                foreach (var sample in profile.Sample)
                {
                    if (!sample.Labels(profile)
                               .Any(l =>
                                    (l.Name == "event") &&
                                    ((l.Value == "thread start") || (l.Value == "thread stop"))))
                    {
                        return false;
                    }

                    // check fake call stack
                    var frames = sample.StackTrace(profile);
                    if (frames.FramesCount != 1)
                    {
                        return false;
                    }

                    if (!(frames[0].Equals(threadStartFrame) || frames[0].Equals(threadStopFrame)))
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
