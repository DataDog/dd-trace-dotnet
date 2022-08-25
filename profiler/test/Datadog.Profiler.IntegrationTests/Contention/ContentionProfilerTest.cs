// <copyright file="ContentionProfilerTest.cs" company="Datadog">
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

namespace Datadog.Profiler.IntegrationTests.Contention
{
    public class ContentionProfilerTest
    {
        private const int ContentionCountSlot = 5;  // defined in enum class SampleValue (Sample.h)
        private const int ContentionDurationSlot = 6;
        private const string ScenarioContention = "--scenario 10 --threads 20";

        private readonly ITestOutputHelper _output;

        public ContentionProfilerTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0" })]
        public void ShouldGetContentionSamples(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioContention);
            runner.Environment.SetVariable(EnvironmentVariables.ContentionProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "0");

            CheckContentionProfiles(runner);
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0" })]
        public void ShouldContentionProfilerBeDisabledByDefault(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioContention);
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "0");

            using var agent = new MockDatadogAgent(_output);

            runner.Run(agent);

            ExtractContentionSamples(runner.Environment.PprofDir).Should().BeEmpty();
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0" })]
        public void ExplicitlyDisableContentionProfiler(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioContention);

            runner.Environment.SetVariable(EnvironmentVariables.ContentionProfilerEnabled, "0");

            using var agent = new MockDatadogAgent(_output);

            runner.Run(agent);

            ExtractContentionSamples(runner.Environment.PprofDir).Should().BeEmpty();
        }

        private static IEnumerable<(long Count, long Duration, StackTrace Stacktrace)> ExtractContentionSamples(string directory)
        {
            static IEnumerable<(long Count, long Duration, StackTrace Stacktrace, long Time)> GetContentionSamples(string directory)
            {
                foreach (var file in Directory.EnumerateFiles(directory, "*.pprof", SearchOption.AllDirectories))
                {
                    using var stream = File.OpenRead(file);
                    var profile = Profile.Parser.ParseFrom(stream);

                    foreach (var sample in profile.Sample)
                    {
                        var count = sample.Value[ContentionCountSlot];
                        if (count == 0)
                        {
                            continue;
                        }

                        var lockCount = sample.Value[ContentionCountSlot];

                        var labels = sample.Labels(profile).ToArray();

                        yield return (count, lockCount, sample.StackTrace(profile), profile.TimeNanos);
                    }
                }
            }

            return GetContentionSamples(directory)
                .OrderBy(s => s.Time)
                .Select(s => (s.Count, s.Duration, s.Stacktrace));
        }

        private void CheckContentionProfiles(TestApplicationRunner runner)
        {
            using var agent = new MockDatadogAgent(_output);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var contentionSamples = ExtractContentionSamples(runner.Environment.PprofDir).ToArray();
            contentionSamples.Should().NotBeEmpty();

            Assert.All(contentionSamples,  s => Assert.True(s.Count > 0));
        }
    }
}
