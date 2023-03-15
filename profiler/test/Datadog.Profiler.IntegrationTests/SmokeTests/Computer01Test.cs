// <copyright file="Computer01Test.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Profiler.IntegrationTests;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.SmokeTests
{
    public class Computer01Test
    {
        private readonly ITestOutputHelper _output;

        public Computer01Test(ITestOutputHelper output)
        {
            _output = output;
        }

        // scenarios implemented in Computer01:
        // -----------------------------------------------------------------------------------------
        //  1: start threads with specific callstacks in another appdomain
        //  2: start threads with generic type and method having long parameters list in callstack
        //  3: start threads that sleep/task.delay for 10s, 20s, 30s, 40s every minute
        //  4: start a thread to compute pi at a certain precision(high CPU usage)
        //  5: start a to compute fibonacci (high CPU usage + deep stacks)
        // -----------------------------------------------------------------------------------------
        [TestAppFact("Samples.Computer01")]
        public void CheckAppDomain(string appName, string framework, string appAssembly)
        {
            var runner = new SmokeTestRunner(appName, framework, appAssembly, commandLine: "--scenario 1", _output);
            runner.RunAndCheck();
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckGenerics(string appName, string framework, string appAssembly)
        {
            var runner = new SmokeTestRunner(appName, framework, appAssembly, commandLine: "--scenario 2", _output);
            runner.RunAndCheck();
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckPi(string appName, string framework, string appAssembly)
        {
            var runner = new SmokeTestRunner(appName, framework, appAssembly, commandLine: "--scenario 4", _output);
            runner.RunAndCheck();
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckFibonacci(string appName, string framework, string appAssembly)
        {
            var runner = new SmokeTestRunner(appName, framework, appAssembly, commandLine: "--scenario 5", _output);
            runner.RunAndCheck();
        }

        [Trait("Category", "LinuxOnly")]
        [TestAppFact("Samples.Computer01")]
        public void CheckAppDomainForOldWayToStackWalk(string appName, string framework, string appAssembly)
        {
            var runner = new SmokeTestRunner(appName, framework, appAssembly, commandLine: "--scenario 1", _output);
            runner.EnvironmentHelper.CustomEnvironmentVariables[EnvironmentVariables.UseBacktrace2] = "0";
            runner.RunAndCheck();
        }

        [Trait("Category", "LinuxOnly")]
        [TestAppFact("Samples.Computer01")]
        public void CheckGenericsForOldWayToStackWalk(string appName, string framework, string appAssembly)
        {
            var runner = new SmokeTestRunner(appName, framework, appAssembly, commandLine: "--scenario 2", _output);
            runner.EnvironmentHelper.CustomEnvironmentVariables[EnvironmentVariables.UseBacktrace2] = "0";
            runner.RunAndCheck();
        }

        [Trait("Category", "LinuxOnly")]
        [TestAppFact("Samples.Computer01")]
        public void CheckPiForOldWayToStackWalk(string appName, string framework, string appAssembly)
        {
            var runner = new SmokeTestRunner(appName, framework, appAssembly, commandLine: "--scenario 4", _output);
            runner.EnvironmentHelper.CustomEnvironmentVariables[EnvironmentVariables.UseBacktrace2] = "0";
            runner.RunAndCheck();
        }

        [Trait("Category", "LinuxOnly")]
        [TestAppFact("Samples.Computer01")]
        public void CheckFibonacciForOldWayToStackWalk(string appName, string framework, string appAssembly)
        {
            var runner = new SmokeTestRunner(appName, framework, appAssembly, commandLine: "--scenario 5", _output);
            runner.EnvironmentHelper.CustomEnvironmentVariables[EnvironmentVariables.UseBacktrace2] = "0";
            runner.RunAndCheck();
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckLinenumbers(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 18");
            runner.Environment.CustomEnvironmentVariables[EnvironmentVariables.DebugInfoEnabled] = "1";

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);
            var expectedStack = new StackTrace(
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:LineNumber |fn:CallThirdMethod"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:LineNumber |fn:CallSecondMethod"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:LineNumber |fn:CallFirstMethod"));

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
                first.StartLine.Should().Equals(103);

                // "normal" line info
                second.Filename.Should().EndWith("LineNumber.cs");
                second.StartLine.Should().Equals(42);

                // hidden debug info
                third.Filename.Should().BeEmpty();
                third.StartLine.Should().Equals(0);
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
                        stackTrace[i].StartLine.Should().Equals(0);
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
                        stackTrace[i].StartLine.Should().Equals(0);
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
