// <copyright file="ExceptionsTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Exceptions
{
    public class ExceptionsTest
    {
        private const string Scenario1 = "--scenario 1";
        private const string Scenario2 = "--scenario 2";
        private const string Scenario3 = "--scenario 3";
        private const string ScenarioMeasureException = "--scenario 6";

        private readonly ITestOutputHelper _output;

        public ExceptionsTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.ExceptionGenerator")]
        public void ThrowExceptionsInParallel(string appName, string framework, string appAssembly)
        {
            StackTrace expectedStack;

            if (framework == "net462")
            {
                expectedStack = new StackTrace(
                    new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ParallelExceptionsScenario |cg: |fn:ThrowExceptions |fg: |sg:(object state)"),
                    new StackFrame("|lm:mscorlib |ns:System.Threading |ct:ThreadHelper |cg: |fn:ThreadStart |fg: |sg:(object obj)"));
            }
            else if (framework == "net6.0")
            {
                expectedStack = new StackTrace(
                    new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ParallelExceptionsScenario |cg: |fn:ThrowExceptions |fg: |sg:(object state)"),
                    new StackFrame("|lm:System.Private.CoreLib |ns:System.Threading |ct:Thread |cg: |fn:StartCallback |fg: |sg:()"));
            }
            else if (framework == "net7.0" || framework == "net8.0")
            {
                expectedStack = new StackTrace(
                    new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ParallelExceptionsScenario |cg: |fn:ThrowExceptions |fg: |sg:(object state)"));
            }
            else
            {
                expectedStack = new StackTrace(
                    new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ParallelExceptionsScenario |cg: |fn:ThrowExceptions |fg: |sg:(object state)"),
                    new StackFrame("|lm:System.Private.CoreLib |ns:System.Threading |ct:ThreadHelper |cg: |fn:ThreadStart |fg: |sg:(object obj)"));
            }

            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Scenario2);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.ExceptionSampleLimit, "10000");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var exceptionSamples = SamplesHelper.ExtractExceptionSamples(runner.Environment.PprofDir).ToArray();

            long total = 0;

            foreach (var sample in exceptionSamples)
            {
                total += sample.Count;
                sample.Type.Should().Be("System.Exception");
                sample.Message.Should().BeEmpty();
                Assert.True(sample.Stacktrace.EndWith(expectedStack));
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
        public void Sampling(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Scenario3);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.ExceptionSampleLimit, "100");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().BeGreaterThan(0);

            var exceptionSamples = SamplesHelper.ExtractExceptionSamples(runner.Environment.PprofDir).ToArray();

            var exceptionCounts = exceptionSamples.GroupBy(s => s.Type)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.Count));

            // We throw 1000 System.Exception exceptions per thread and we have 4 threads.
            // The profiler samples the exception but also upscale the values after.
            // So we just check that we are in the right order of magnitude.
            // Note: with timestamps, upscaling will round down due to the lack of aggregation
            exceptionCounts.Should().ContainKey("System.Exception").WhoseValue.Should().BeCloseTo(4000, 300);

            // System.InvalidOperationException is seen only once, so it should be sampled
            // despite the sampler being saturated by the 4000 System.Exception
            exceptionCounts.Should().ContainKey("System.InvalidOperationException").WhoseValue.Should().Be(1);
        }

        [TestAppFact("Samples.ExceptionGenerator")]
        public void GetExceptionSamplesWithTimestamp(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Scenario1);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            // timestamps are enabled by default
            runner.Environment.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "1");

            CheckExceptionProfiles(runner, true);
        }

        [TestAppFact("Samples.ExceptionGenerator")]
        public void GetExceptionSamplesWithoutTimestamp(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Scenario1);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.TimestampsAsLabelEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "1");

            CheckExceptionProfiles(runner, false);
        }

        [TestAppFact("Samples.ExceptionGenerator")]
        public void ExceptionProfilerIsEnabledByDefault(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Scenario1);

            // Test that the exception profiler is enabled by default
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.GarbageCollectionProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.ContentionProfilerEnabled, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            // On alpine, this check is flaky.
            // Disable it on alpine for now
            if (!EnvironmentHelper.IsAlpine)
            {
                Assert.True(agent.NbCallsOnProfilingEndpoint > 0);
            }

            // only exception profiler enabled so should see 1 value per sample
            SamplesHelper.CheckSamplesValueCount(runner.Environment.PprofDir, 1);
        }

        [TestAppFact("Samples.ExceptionGenerator")]
        public void ExplicitlyDisableExceptionProfiler(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Scenario1);

            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            // On alpine, this check is flaky.
            // Disable it on alpine for now
            if (!EnvironmentHelper.IsAlpine)
            {
                Assert.True(agent.NbCallsOnProfilingEndpoint > 0);
            }

            // only walltime profiler enabled so should see 1 value per sample
            SamplesHelper.CheckSamplesValueCount(runner.Environment.PprofDir, 1);
        }

        [TestAppFact("Samples.ExceptionGenerator")]
        public void MeasureExceptions(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioMeasureException);

            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var exceptionSamples = SamplesHelper.ExtractExceptionSamples(runner.Environment.PprofDir).ToArray();
            exceptionSamples.Should().NotBeEmpty();

            // this test always succeeds: it is used to display the differences between sampled and real exceptions
            Dictionary<string, int> profiledExceptions = GetProfiledExceptions(exceptionSamples);
            Dictionary<string, int> realExceptions = GetRealExceptions(runner.ProcessOutput);

            _output.WriteLine("Comparing exceptions");
            _output.WriteLine("-------------------------------------------------------");
            _output.WriteLine("      Count          Type");
            _output.WriteLine("-------------------------------------------------------");
            foreach (var exception in profiledExceptions)
            {
                var exceptionCount = exception.Value;
                var type = exception.Key;
                int pos = type.LastIndexOf('.');
                if (pos != -1)
                {
                    type = type.Substring(pos + 1);
                }

                // TODO: dump real and profiled count
                if (!realExceptions.TryGetValue(type, out var stats))
                {
                    continue;
                }

                StringBuilder builder = new StringBuilder();
                builder.AppendLine($"{exceptionCount,11} {type}");
                builder.AppendLine($"{stats,11}");
                _output.WriteLine(builder.ToString());
            }
        }

        private static Dictionary<string, int> GetRealExceptions(string output)
        {
            const string startToken = "Exceptions start";
            const string endToken = "Exceptions end";

            var realExceptions = new Dictionary<string, int>();
            if (output == null)
            {
                return realExceptions;
            }

            // look for the following sections with type=count,size
            /*
                Exceptions start
                ArgumentException=8345
                SystemException=8383
                InvalidOperationException=8276
                InvalidCastException=8346
                TimeoutException=8353
                BadImageFormatException=8368
                NotImplementedException=8270
                ArithmeticException=8293
                IndexOutOfRangeException=8349
                NotSupportedException=8393
                RankException=8357
                UnauthorizedAccessException=8267
                Exceptions end
            */
            int pos = 0;
            int next = 0;
            int end = 0;
            while (true)
            {
                // look for an exceptions block
                next = output.IndexOf(startToken, pos);
                if (next == -1)
                {
                    break;
                }

                next += startToken.Length;
                next = GotoEoL(output, next);
                if (next == -1)
                {
                    break;
                }

                pos = next + 1; // point to the beginning of the first exception stats

                // look for the end of the exceptions block
                end = output.IndexOf(endToken, pos);
                if (end == -1)
                {
                    break;
                }

                // extract line by line the exception stats from this block
                int eol = 0;
                while (true)
                {
                    next = GotoEoL(output, pos);
                    if (next == -1)
                    {
                        break;
                    }

                    // handle Windows (\r\n) and Linux (\n) cases
                    if (output[next - 1] == '\r')
                    {
                        eol = next - 1;
                    }
                    else
                    {
                        eol = next;
                    }

                    // extract type and count
                    //   ArgumentException=3879
                    var line = output.AsSpan(pos, eol - pos);

                    // get type name
                    var current = output.IndexOf('=', pos);
                    if (current == -1)
                    {
                        next = -1;
                        break;
                    }

                    var name = output.Substring(pos, current - pos);
                    pos = current + 1;
                    if (pos >= end)
                    {
                        next = -1;
                        break;
                    }

                    // get count
                    var text = output.AsSpan(pos, eol - pos);
                    var count = int.Parse(text);

                    // add the stats
                    if (!realExceptions.TryGetValue(name, out var stats))
                    {
                        realExceptions.Add(name, 0);
                    }

                    stats += count;
                    realExceptions[name] = stats;

                    // goto the next line (or the end token)
                    pos = next + 1;

                    // check for the last stats
                    if (next == (end - 1))
                    {
                        break;
                    }
                }

                if (next == -1)
                {
                    break;
                }
            }

            return realExceptions;
        }

        private static int GotoEoL(string text, int pos)
        {
            var next = text.IndexOf('\n', pos);
            return next;
        }

        private static Dictionary<string, int> GetProfiledExceptions(IEnumerable<(string Type, string Message, long Count, StackTrace Stacktrace)> exceptions)
        {
            var profiledExceptions = new Dictionary<string, int>();

            foreach (var exception in exceptions)
            {
                if (!profiledExceptions.TryGetValue(exception.Type, out var stats))
                {
                    stats = 0;
                    profiledExceptions.Add(exception.Type, 0);
                }

                stats += (int)exception.Count;
                profiledExceptions[exception.Type] = stats;
            }

            return profiledExceptions;
        }

        private void CheckExceptionProfiles(TestApplicationRunner runner, bool withTimestamps)
        {
            var stack1 = new StackTrace(
                new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ExceptionsProfilerTestScenario |cg: |fn:Throw1_2 |fg: |sg:(System.Exception ex)"),
                new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ExceptionsProfilerTestScenario |cg: |fn:Throw1_1 |fg: |sg:(System.Exception ex)"),
                new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ExceptionsProfilerTestScenario |cg: |fn:Throw1 |fg: |sg:(System.Exception ex)"),
                new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ExceptionsProfilerTestScenario |cg: |fn:Run |fg: |sg:()"),
                new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:Program |cg: |fn:Main |fg: |sg:(string[] args)"));

            var stack2 = new StackTrace(
                new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ExceptionsProfilerTestScenario |cg: |fn:Throw2_3 |fg: |sg:(System.Exception ex)"),
                new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ExceptionsProfilerTestScenario |cg: |fn:Throw2_2 |fg: |sg:(System.Exception ex)"),
                new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ExceptionsProfilerTestScenario |cg: |fn:Throw2_1 |fg: |sg:(System.Exception ex)"),
                new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ExceptionsProfilerTestScenario |cg: |fn:Throw2 |fg: |sg:(System.Exception ex)"),
                new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ExceptionsProfilerTestScenario |cg: |fn:Run |fg: |sg:()"),
                new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:Program |cg: |fn:Main |fg: |sg:(string[] args)"));

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var exceptionSamples = SamplesHelper.ExtractExceptionSamples(runner.Environment.PprofDir).ToArray();

            if (withTimestamps)
            {
                // no possible aggregation
                exceptionSamples.Should().BeEquivalentTo(
                    new List<(string, string, int, StackTrace)>
                    {
                        ("System.InvalidOperationException", "IOE", 1, stack1),
                        ("System.InvalidOperationException", "IOE", 1, stack1),
                        ("System.NotSupportedException", "NSE", 1, stack1),
                        ("System.NotSupportedException", "NSE", 1, stack1),
                        ("System.NotImplementedException", "NIE", 1, stack1),
                        ("System.NotImplementedException", "NIE", 1, stack2),
                        ("System.Exception", "E1", 1, stack1),
                        ("System.Exception", "E2", 1, stack1)
                    });
            }
            else
            {
                // IOE and NSE exceptions will be aggregated
                exceptionSamples.Should().BeEquivalentTo(
                    new List<(string, string, int, StackTrace)>
                    {
                        ("System.InvalidOperationException", "IOE", 2, stack1),
                        ("System.NotSupportedException", "NSE", 2, stack1),
                        ("System.NotImplementedException", "NIE", 1, stack1),
                        ("System.NotImplementedException", "NIE", 1, stack2),
                        ("System.Exception", "E1", 1, stack1),
                        ("System.Exception", "E2", 1, stack1)
                    });
            }
        }
    }
}
