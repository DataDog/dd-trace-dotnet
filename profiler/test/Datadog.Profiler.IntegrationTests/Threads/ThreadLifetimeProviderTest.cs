// <copyright file="ThreadLifetimeProviderTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.IntegrationTests.Helpers.Profiles;
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

        [TestAppFact(TestApplication.Computer01, /*TestFramework.Net48,*/ TestFramework.Net6, TestFramework.Net7)]
        public void ShouldGetThreadLifetimeSamples(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: ScenarioGenerics);
            runner.Environment.EnableOnly(ProfilerFeature.Timeline);

            using var run = runner.Run();

            // only thread lifetime profiler is enabled so should only see the corresponding samples
            Assert.True(run.UploadCount > 0);
            Assert.True(run.Profiles.AllSamplesMatch(IsThreadLifetimeSample));
        }

        [TestAppFact(TestApplication.Computer01, /*TestFramework.Net48,*/ TestFramework.Net6, TestFramework.Net7)]
        public void ShouldNotGetThreadLifetimeSamplesByDefault(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: ScenarioGenerics);
            runner.Environment.EnableOnly();

            using var run = runner.Run();

            Assert.False(run.UploadCount > 0);
            Assert.False(run.Profiles.AllSamplesMatch(IsThreadLifetimeSample));
        }

        private static bool IsThreadLifetimeSample(ProfileSample sample)
        {
            var events = sample.Labels.GetValues(ProfileLabelNames.Event);
            if (!events.Contains("thread start") && !events.Contains("thread stop"))
            {
                return false;
            }

            // check fake call stack
            var frames = sample.CallStack;
            return frames.FramesCount == 1
                && (frames[0].Equals(new StackFrame(ThreadStartFrame)) || frames[0].Equals(new StackFrame(ThreadStopFrame)));
        }
    }
}
