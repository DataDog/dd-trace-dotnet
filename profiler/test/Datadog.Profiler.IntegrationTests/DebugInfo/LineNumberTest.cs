// <copyright file="LineNumberTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.IntegrationTests.Helpers.Profiles;
using Datadog.Profiler.IntegrationTests.Xunit;
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

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void CheckLinenumbers(TestAppContext app)
        {
            if (EnvironmentHelper.IsAlpine)
            {
                throw new SkipTestException("This test is skipped on Alpine until the stack walking issue is fixed.");
            }

            var runner = app.CreateRunner(_output, commandLine: "--scenario 18");
            runner.Environment.SetVariable(EnvironmentVariables.DebugInfoEnabled, "1");

            using var run = runner.Run();
            var expectedStack = new StackTrace(
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:LineNumber |cg: |fn:CallThirdMethod |fg: |sg:()"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:LineNumber |cg: |fn:CallSecondMethod |fg: |sg:()"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:LineNumber |cg: |fn:CallFirstMethod |fg: |sg:()"));

            var samples = run.Profiles.CallStacks.Where(stackTrace => stackTrace.Contains(expectedStack)).ToArray();

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
                first.Line.Should().Be(103);

                // "normal" line info
                second.Filename.Should().EndWith("LineNumber.cs");
                second.Line.Should().Be(41);

                // hidden debug info
                third.Filename.Should().BeEmpty();
                third.Line.Should().Be(0);
            }
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void CheckNoLinenumbersIfDisabled(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 18");
            runner.Environment.SetVariable(EnvironmentVariables.DebugInfoEnabled, "0");

            using var run = runner.Run();

            AssertNoLineNumbers(run.Profiles);
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void CheckLineNumberAreDisabledByDefault(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 18");

            using var run = runner.Run();

            AssertNoLineNumbers(run.Profiles);
        }

        private static void AssertNoLineNumbers(ProfileOutput profiles)
        {
            foreach (var stackTrace in profiles.CallStacks)
            {
                for (var i = 0; i < stackTrace.FramesCount; i++)
                {
                    stackTrace[i].Filename.Should().BeEmpty();
                    stackTrace[i].Line.Should().Be(0);
                }
            }
        }
    }
}
