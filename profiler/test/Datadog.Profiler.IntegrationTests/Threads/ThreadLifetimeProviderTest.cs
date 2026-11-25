// <copyright file="ThreadLifetimeProviderTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.IntegrationTests.Helpers.Profiles;
using FluentAssertions;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Threads
{
    public class ThreadLifetimeProviderTest
    {
        private const string ScenarioGenerics = "--scenario 14";
        private static readonly StackFrame ThreadStartFrame = StackFrame.Build().Function("Thread Start");
        private static readonly StackFrame ThreadStopFrame = StackFrame.Build().Function("Thread Stop");

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
            run.UploadCount.Should().BeGreaterThan(0);
            run.Profiles.Profiles.Should().NotBeEmpty().And.OnlyContain(profile => profile.Samples.Count > 0);
            run.Profiles.Samples.Should().OnlyContain(sample => IsThreadLifetimeSample(sample));
        }

        [TestAppFact(TestApplication.Computer01, /*TestFramework.Net48,*/ TestFramework.Net6, TestFramework.Net7)]
        public void ShouldNotGetThreadLifetimeSamplesByDefault(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: ScenarioGenerics);
            runner.Environment.EnableOnly();

            using var run = runner.Run();

            run.UploadCount.Should().Be(0);
            run.Profiles.Samples.Should().NotContain(sample => IsThreadLifetimeSample(sample));
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
                && (frames[0].Equals(ThreadStartFrame) || frames[0].Equals(ThreadStopFrame));
        }
    }
}
