// <copyright file="ExceptionsTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.SmokeTests;
using FluentAssertions;
using Perftools.Profiles.Tests;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Exceptions
{
    public class ExceptionsTest
    {
        private const string CommandLine = "--scenario ExceptionsProfilerTestScenario";
        private const int ExceptionsSlot = 2;  // defined in enum class SampleValue (Sample.h)

        private readonly ITestOutputHelper _output;

        public ExceptionsTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Datadog.Demos.ExceptionGenerator")]
        public void GetExceptionSamples(string appName, string framework, string appAssembly)
        {
            using var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: CommandLine, enableNewPipeline: true);
            CheckExceptionProfiles(runner);
        }

        private static IEnumerable<(string Type, string Message, long Count)> ExtractExceptionSamples(string directory)
        {
            static IEnumerable<(string Type, string Message, long Count, long Time)> SamplesWithTimestamp(string directory)
            {
                foreach (var file in Directory.EnumerateFiles(directory, "*.pprof", SearchOption.AllDirectories))
                {
                    using var stream = File.OpenRead(file);

                    var profile = Profile.Parser.ParseFrom(stream);

                    foreach (var sample in profile.Sample)
                    {
                        var count = sample.Value[ExceptionsSlot];

                        if (count == 0)
                        {
                            continue;
                        }

                        var labels = sample.Labels(profile).ToArray();

                        var type = labels.Single(l => l.Name == "exception type").Value;
                        var message = labels.Single(l => l.Name == "exception message").Value;

                        yield return (type, message, count, profile.TimeNanos);
                    }
                }
            }

            return SamplesWithTimestamp(directory)
                .OrderBy(s => s.Time)
                .Select(s => (s.Type, s.Message, s.Count));
        }

        private void CheckExceptionProfiles(TestApplicationRunner runner)
        {
            using var agent = new MockDatadogAgent(_output);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var exceptionSamples = ExtractExceptionSamples(runner.Environment.PprofDir).ToArray();

            exceptionSamples.Should().HaveCount(6);

            exceptionSamples[0].Should().Be(("System.InvalidOperationException", "IOE", 2));
            exceptionSamples[1].Should().Be(("System.NotSupportedException", "NSE", 2));
            exceptionSamples[2].Should().Be(("System.NotImplementedException", "NIE", 1));
            exceptionSamples[3].Should().Be(("System.NotImplementedException", "NIE", 1));
            exceptionSamples[4].Should().Be(("System.Exception", "E1", 1));
            exceptionSamples[5].Should().Be(("System.Exception", "E2", 1));
        }
    }
}
