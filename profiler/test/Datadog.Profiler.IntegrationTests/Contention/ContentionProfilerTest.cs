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
        private const int ContentionCountSlot = 3;  // defined in enum class SampleValue (Sample.h)
        private const int ContentionDurationSlot = 4;
        private const string ScenarioContention = "--scenario 10 --threads 20";

        private readonly ITestOutputHelper _output;

        public ContentionProfilerTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01")]
        public void ShouldGetContentionSamples(string appName, string framework, string appAssembly)
        {
            // only valid with .NET 5+
            if (framework != "net6.0")
            {
                // TODO: find a way to skip the test based on the framework (here .NET 5+ only)
                return;
            }

            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioContention);
            runner.Environment.SetVariable(EnvironmentVariables.ContentionProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "0");

            CheckContentionProfiles(runner);
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

                        var size = sample.Value[ContentionDurationSlot];

                        var labels = sample.Labels(profile).ToArray();

                        yield return (count, size, sample.StackTrace(profile), profile.TimeNanos);
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

            Assert.Collection(contentionSamples, s => Assert.True(s.Duration > 0));
        }
    }
}
