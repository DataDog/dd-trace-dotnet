// <copyright file="LineNumberTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.DebugInfo
{
    public class LineNumberTest
    {
        private readonly ITestOutputHelper _output;

        public LineNumberTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckLinenumbers(string appName, string framework, string appAssembly)
        {
            if (EnvironmentHelper.IsAlpine)
            {
                // skip the test on Alpine for now: "This test is skipped on Alpine for now. TODO: fix stackwalking issue on alpine."
                return;
            }

            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 18");
            runner.Environment.CustomEnvironmentVariables[EnvironmentVariables.DebugInfoEnabled] = "1";

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);
            var expectedStack = new StackTrace(
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:LineNumber |cg: |fn:CallThirdMethod |fg: |sg:()"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:LineNumber |cg: |fn:CallSecondMethod |fg: |sg:()"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:LineNumber |cg: |fn:CallFirstMethod |fg: |sg:()"));

            var samples = ExtractCallStackMatching(runner.Environment.PprofDir, expectedStack).ToArray();

            samples.Should().NotBeEmpty();

            foreach (var sample in samples)
            {
                StackFrame first = default;
                StackFrame second = default;
                StackFrame third = default;

                for (var i = 0; i < sample.FramesCount; i++)
                {
                    if (sample[i].Function == "CallFirstMethod")
                    {
                        first = sample[i];
                    }

                    if (sample[i].Function == "CallSecondMethod")
                    {
                        second = sample[i];
                    }

                    if (sample[i].Function == "CallThirdMethod")
                    {
                        third = sample[i];
                    }
                }

                // forced line info
                first.Filename.Should().EndWith("LineNumber.cs");
                first.StartLine.Should().Be(103);
                first.Line.Should().Be(103);

                // "normal" line info
                second.Filename.Should().EndWith("LineNumber.cs");
                second.StartLine.Should().Be(41);
                second.Line.Should().Be(41);

                // hidden debug info
                third.Filename.Should().BeEmpty();
                third.StartLine.Should().Be(0);
                third.Line.Should().Be(0);
            }
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckNoLinenumbersIfDisabled(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 18");
            runner.Environment.CustomEnvironmentVariables[EnvironmentVariables.DebugInfoEnabled] = "0";

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            foreach (var profile in SamplesHelper.GetProfiles(runner.Environment.PprofDir))
            {
                foreach (var sample in profile.Sample)
                {
                    var stackTrace = sample.StackTrace(profile);
                    for (var i = 0; i < stackTrace.FramesCount; i++)
                    {
                        stackTrace[i].Filename.Should().BeEmpty();
                        stackTrace[i].StartLine.Should().Be(0);
                    }
                }
            }
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckLineNumberAreDisabledByDefault(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 18");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            foreach (var profile in SamplesHelper.GetProfiles(runner.Environment.PprofDir))
            {
                foreach (var sample in profile.Sample)
                {
                    var stackTrace = sample.StackTrace(profile);
                    for (var i = 0; i < stackTrace.FramesCount; i++)
                    {
                        stackTrace[i].Filename.Should().BeEmpty();
                        stackTrace[i].StartLine.Should().Be(0);
                    }
                }
            }
        }

        private static IEnumerable<StackTrace> ExtractCallStackMatching(string pprofDif, StackTrace expectedStackTrace)
        {
            foreach (var profile in SamplesHelper.GetProfiles(pprofDif))
            {
                foreach (var sample in profile.Sample)
                {
                    var stackTrace = sample.StackTrace(profile);
                    if (stackTrace.Contains(expectedStackTrace))
                    {
                        yield return stackTrace;
                    }
                }
            }
        }
    }
}
