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
        private const string Scenario1 = "--scenario 1";
        private const string Scenario2 = "--scenario 2";
        private const int ExceptionsSlot = 2;  // defined in enum class SampleValue (Sample.h)

        private readonly ITestOutputHelper _output;

        public ExceptionsTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.ExceptionGenerator")]
        public void ThrowExceptionsInParallel(string appName, string framework, string appAssembly)
        {
            StackTrace expectedStack;

            if (framework == "net45")
            {
                expectedStack = new StackTrace(
                    new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ParallelExceptionsScenario |fn:ThrowExceptions"),
                    new StackFrame("|lm:mscorlib |ns:System.Threading |ct:ThreadHelper |fn:ThreadStart"));
            }
            else
            {
                expectedStack = new StackTrace(
                    new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ParallelExceptionsScenario |fn:ThrowExceptions"),
                    new StackFrame("|lm:System.Private.CoreLib |ns:System.Threading |ct:ThreadHelper |fn:ThreadStart"));
            }

            using var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Scenario2);
            runner.Environment.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "1");

            using var agent = new MockDatadogAgent(_output);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var exceptionSamples = ExtractExceptionSamples(runner.Environment.PprofDir).ToArray();

            long total = 0;

            foreach (var sample in exceptionSamples)
            {
                total += sample.Count;
                sample.Type.Should().Be("System.Exception");
                sample.Message.Should().BeEmpty();
                sample.Stacktrace.Should().Be(expectedStack);
            }

            foreach (var file in Directory.GetFiles(runner.Environment.LogDir))
            {
                _output.WriteLine($"Log file: {file}");
            }

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
                .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            // Stackwalk will fail if the walltime profiler tries to inspect the thread at the same time as the exception profiler
            // This is expected so we remove those from the expected count
            var missedExceptions = File.ReadLines(logFile)
                .Count(l => l.Contains("Failed to walk stack for thrown exception: CORPROF_E_STACKSNAPSHOT_UNSAFE (80131360)"));

            int expectedExceptionCount = (4 * 1000) - missedExceptions;

            expectedExceptionCount.Should().BeGreaterThan(0, "only a few exceptions should be missed");

            total.Should().Be(expectedExceptionCount);
        }

        [TestAppFact("Samples.ExceptionGenerator")]
        public void GetExceptionSamples(string appName, string framework, string appAssembly)
        {
            using var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Scenario1);
            runner.Environment.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "1");

            CheckExceptionProfiles(runner);
        }

        [TestAppFact("Samples.ExceptionGenerator")]
        public void DisableExceptionProfiler(string appName, string framework, string appAssembly)
        {
            using var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Scenario1);

            // Test that the exception profiler is disabled by default.

            using var agent = new MockDatadogAgent(_output);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            ExtractExceptionSamples(runner.Environment.PprofDir).Should().BeEmpty();
        }

        [TestAppFact("Samples.ExceptionGenerator")]
        public void ExplicitlyDisableExceptionProfiler(string appName, string framework, string appAssembly)
        {
            using var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Scenario1);

            runner.Environment.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "0");

            using var agent = new MockDatadogAgent(_output);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            ExtractExceptionSamples(runner.Environment.PprofDir).Should().BeEmpty();
        }

        private static IEnumerable<(string Type, string Message, long Count, StackTrace Stacktrace)> ExtractExceptionSamples(string directory)
        {
            static IEnumerable<(string Type, string Message, long Count, StackTrace Stacktrace, long Time)> SamplesWithTimestamp(string directory)
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

                        yield return (type, message, count, sample.StackTrace(profile), profile.TimeNanos);
                    }
                }
            }

            return SamplesWithTimestamp(directory)
                .OrderBy(s => s.Time)
                .Select(s => (s.Type, s.Message, s.Count, s.Stacktrace));
        }

        private void CheckExceptionProfiles(TestApplicationRunner runner)
        {
            var stack1 = new StackTrace(
                new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ExceptionsProfilerTestScenario |fn:Throw1_2"),
                new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ExceptionsProfilerTestScenario |fn:Throw1_1"),
                new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ExceptionsProfilerTestScenario |fn:Throw1"),
                new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ExceptionsProfilerTestScenario |fn:Run"),
                new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:Program |fn:Main"));

            var stack2 = new StackTrace(
                new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ExceptionsProfilerTestScenario |fn:Throw2_3"),
                new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ExceptionsProfilerTestScenario |fn:Throw2_2"),
                new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ExceptionsProfilerTestScenario |fn:Throw2_1"),
                new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ExceptionsProfilerTestScenario |fn:Throw2"),
                new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ExceptionsProfilerTestScenario |fn:Run"),
                new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:Program |fn:Main"));

            using var agent = new MockDatadogAgent(_output);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var exceptionSamples = ExtractExceptionSamples(runner.Environment.PprofDir).ToArray();

            exceptionSamples.Should().HaveCount(6);

            exceptionSamples[0].Should().Be(("System.InvalidOperationException", "IOE", 2, stack1));
            exceptionSamples[1].Should().Be(("System.NotSupportedException", "NSE", 2, stack1));
            exceptionSamples[2].Should().Be(("System.NotImplementedException", "NIE", 1, stack1));
            exceptionSamples[3].Should().Be(("System.NotImplementedException", "NIE", 1, stack2));
            exceptionSamples[4].Should().Be(("System.Exception", "E1", 1, stack1));
            exceptionSamples[5].Should().Be(("System.Exception", "E2", 1, stack1));
        }
    }
}
