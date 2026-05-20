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
using Datadog.Profiler.IntegrationTests.Xunit;
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

        [Flaky("Flaky on ARM64")]
        [TestAppFact("Samples.ExceptionGenerator")]
        public void ThrowExceptionsInParallel(string appName, string framework, string appAssembly)
        {
            StackTrace expectedStack;

            if (framework == "net48")
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
            else if (framework == "net9.0")
            {
                if (IntPtr.Size == 4)
                {
                    // 32-bit
                    expectedStack = new StackTrace(
                        new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ParallelExceptionsScenario |cg: |fn:ThrowExceptions |fg: |sg:(object state)"),
                        new StackFrame("|lm:System.Private.CoreLib |ns:System.Threading |ct:Thread |cg: |fn:StartCallback |fg: |sg:()"));
                }
                else
                {
                    // 64 bit
                    expectedStack = new StackTrace(
                        new StackFrame("|lm:System.Private.CoreLib |ns:System.Runtime |ct:EH |cg: |fn:DispatchEx |fg: |sg:(System.Runtime.StackFrameIterator& frameIter, ExInfo& exInfo)"),
                        new StackFrame("|lm:System.Private.CoreLib |ns:System.Runtime |ct:EH |cg: |fn:RhThrowEx |fg: |sg:(object exceptionObj, ExInfo& exInfo)"),
                        new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ParallelExceptionsScenario |cg: |fn:ThrowExceptions |fg: |sg:(object state)"));
                }
            }
            else if (framework =="net10.0")
            {
                if (IntPtr.Size == 4)
                {
                    // 32-bit
                    expectedStack = new StackTrace(
                        new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ParallelExceptionsScenario |cg: |fn:ThrowExceptions |fg: |sg:(object state)"),
                        new StackFrame("|lm:System.Private.CoreLib |ns:System.Threading |ct:Thread |cg: |fn:StartCallback |fg: |sg:()"));
                }
                else
                {
                    // 64 bit
                    expectedStack = new StackTrace(
                        new StackFrame("|lm:System.Private.CoreLib |ns:System.Runtime |ct:EH |cg: |fn:DispatchEx |fg: |sg:(System.Runtime.StackFrameIterator& frameIter, ExInfo& exInfo)"),
                        new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ParallelExceptionsScenario |cg: |fn:ThrowExceptions |fg: |sg:(object state)"));
                }
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

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var exceptionSamples = SamplesHelper.ExtractExceptionSamples(runner.Environment.PprofDir).ToArray();

            long total = 0;

            foreach (var sample in exceptionSamples)
            {
                total += sample.Count;
                sample.Type.Should().Be("System.Exception");
                sample.Message.Should().BeEmpty();
                var (matched, message) = AssertExpectedStack(sample.Stacktrace, expectedStack);
                Assert.True(matched, message);
            }

            foreach (var file in Directory.GetFiles(runner.Environment.LogDir))
            {
                runner.XUnitLogger.WriteLine($"Log file: {file}");
            }

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
                .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            // Stackwalk will fail if the walltime profiler tries to inspect the thread at the same time as the exception profiler
            // This is expected so we remove those from the expected count
            var missedExceptions = File.ReadLines(logFile)
                .Count(l => l.Contains("Failed to walk stack for thrown exception: CORPROF_E_STACKSNAPSHOT_UNSAFE (80131360)"));

            int expectedExceptionCount = (4 * 1000) - missedExceptions;

            expectedExceptionCount.Should().BeGreaterThan(0, "only a few exceptions should be missed");

            if (EnvironmentHelper.GetPlatform() == "ARM64")
            {
                // On ARM64, we may skip some callstack (failed to identify frame type while skipping native frames)
                total.Should().BeGreaterThan(expectedExceptionCount - 100);
            }
            else
            {
                total.Should().Be(expectedExceptionCount);
            }
        }
    
        [Flaky("Flaky on ARM64")]
        [TestAppFact("Samples.ExceptionGenerator")]
        public void ThrowExceptionsInParallelWithCustomGetFunctionFromIp(string appName, string framework, string appAssembly)
        {
            StackTrace expectedStack;

            if (framework == "net48")
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
            else if (framework == "net9.0")
            {
                if (IntPtr.Size == 4)
                {
                    // 32-bit
                    expectedStack = new StackTrace(
                        new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ParallelExceptionsScenario |cg: |fn:ThrowExceptions |fg: |sg:(object state)"),
                        new StackFrame("|lm:System.Private.CoreLib |ns:System.Threading |ct:Thread |cg: |fn:StartCallback |fg: |sg:()"));
                }
                else
                {
                    // 64 bit
                    expectedStack = new StackTrace(
                        new StackFrame("|lm:System.Private.CoreLib |ns:System.Runtime |ct:EH |cg: |fn:DispatchEx |fg: |sg:(System.Runtime.StackFrameIterator& frameIter, ExInfo& exInfo)"),
                        new StackFrame("|lm:System.Private.CoreLib |ns:System.Runtime |ct:EH |cg: |fn:RhThrowEx |fg: |sg:(object exceptionObj, ExInfo& exInfo)"),
                        new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ParallelExceptionsScenario |cg: |fn:ThrowExceptions |fg: |sg:(object state)"));
                }
            }
            else if (framework == "net10.0")
            {
                if (IntPtr.Size == 4)
                {
                    // 32-bit
                    expectedStack = new StackTrace(
                        new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ParallelExceptionsScenario |cg: |fn:ThrowExceptions |fg: |sg:(object state)"),
                        new StackFrame("|lm:System.Private.CoreLib |ns:System.Threading |ct:Thread |cg: |fn:StartCallback |fg: |sg:()"));
                }
                else
                {
                    // 64 bit
                    expectedStack = new StackTrace(
                        new StackFrame("|lm:System.Private.CoreLib |ns:System.Runtime |ct:EH |cg: |fn:DispatchEx |fg: |sg:(System.Runtime.StackFrameIterator& frameIter, ExInfo& exInfo)"),
                        new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ParallelExceptionsScenario |cg: |fn:ThrowExceptions |fg: |sg:(object state)"));
                }
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
            runner.Environment.SetVariable(EnvironmentVariables.UseManagedCodeCache, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var exceptionSamples = SamplesHelper.ExtractExceptionSamples(runner.Environment.PprofDir).ToArray();

            long total = 0;

            foreach (var sample in exceptionSamples)
            {
                total += sample.Count;
                sample.Type.Should().Be("System.Exception");
                sample.Message.Should().BeEmpty();
                var (matched, message) = AssertExpectedStack(sample.Stacktrace, expectedStack);
                Assert.True(matched, message);
            }

            foreach (var file in Directory.GetFiles(runner.Environment.LogDir))
            {
                runner.XUnitLogger.WriteLine($"Log file: {file}");
            }

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
                .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            // Stackwalk will fail if the walltime profiler tries to inspect the thread at the same time as the exception profiler
            // This is expected so we remove those from the expected count
            var missedExceptions = File.ReadLines(logFile)
                .Count(l => l.Contains("Failed to walk stack for thrown exception: CORPROF_E_STACKSNAPSHOT_UNSAFE (80131360)"));

            int expectedExceptionCount = (4 * 1000) - missedExceptions;

            expectedExceptionCount.Should().BeGreaterThan(0, "only a few exceptions should be missed");

            if (EnvironmentHelper.GetPlatform() == "ARM64")
            {
                // On ARM64, we may skip some callstack (failed to identify frame type while skipping native frames)
                total.Should().BeGreaterThan(expectedExceptionCount - 100);
            }
            else
            {
                total.Should().Be(expectedExceptionCount);
            }
        }

        [Flaky("Flaky on ARM64")]
        [Trait("Category", "LinuxOnly")]
        [TestAppFact("Samples.ExceptionGenerator", new[] { "net48", "netcoreapp3.1", "net6.0", "net8.0", })] // FIXME: .NET 9 skipping .NET 9 for now
        public void ThrowExceptionsInParallelWithNewCpuProfiler(string appName, string framework, string appAssembly)
        {
            StackTrace expectedStack;

            if (framework == "net48")
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
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerType, "TimerCreate");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var exceptionSamples = SamplesHelper.GetSamples(runner.Environment.PprofDir, "exception").ToArray();

            long total = exceptionSamples.Length;

            foreach (var (stackTrace, labels, _) in exceptionSamples)
            {
                labels.Should().ContainSingle(x => x.Name == "exception type" && x.Value == "System.Exception");
                labels.Should().ContainSingle(x => x.Name == "exception message" && string.IsNullOrWhiteSpace(x.Value));
                var (matched, message) = AssertExpectedStack(stackTrace, expectedStack);
                Assert.True(matched, message);
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

            if (EnvironmentHelper.GetPlatform() == "ARM64")
            {
                // On ARM64, we may skip some callstack (failed to identify frame type while skipping native frames)
                total.Should().BeGreaterThan(expectedExceptionCount - 100);
            }
            else
            {
                total.Should().Be(expectedExceptionCount);
            }
        }

        [TestAppFact("Samples.ExceptionGenerator")]
        public void Sampling(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Scenario3);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.ExceptionSampleLimit, "100");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
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

        [Flaky("Flaky on ARM64")]
        [TestAppFact("Samples.ExceptionGenerator", new[] { "net48", "netcoreapp3.1", "net6.0", "net8.0", })] // FIXME: .NET 9 skipping .NET 9 for now
        public void GetExceptionSamplesWithTimestamp(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Scenario1);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            // timestamps are enabled by default
            runner.Environment.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "1");

            CheckExceptionProfiles(runner, true);
        }

        [Flaky("Flaky on ARM64")]
        [TestAppFact("Samples.ExceptionGenerator", new[] { "net48", "netcoreapp3.1", "net6.0", "net8.0", })] // FIXME: .NET 9 skipping .NET 9 for now
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
            runner.Environment.SetVariable(EnvironmentVariables.GcThreadsCpuTimeEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.ThreadLifetimeEnabled, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

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

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

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

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var exceptionSamples = SamplesHelper.ExtractExceptionSamples(runner.Environment.PprofDir).ToArray();
            exceptionSamples.Should().NotBeEmpty();

            // this test always succeeds: it is used to display the differences between sampled and real exceptions
            Dictionary<string, int> profiledExceptions = GetProfiledExceptions(exceptionSamples);
            Dictionary<string, int> realExceptions = GetRealExceptions(runner.ProcessOutput);

            var logger = runner.XUnitLogger;
            logger.WriteLine("Comparing exceptions");
            logger.WriteLine("-------------------------------------------------------");
            logger.WriteLine("      Count          Type");
            logger.WriteLine("-------------------------------------------------------");
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
                logger.WriteLine(builder.ToString());
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

        private static (bool Matched, string Message) AssertExpectedStack_x86_64(StackTrace actualStack, StackTrace expectedStack)
        {
            if (!actualStack.EndWith(expectedStack, out var failureDetail))
            {
                return (false, failureDetail);
            }

            return (true, string.Empty);
        }

        // For ARM64, we need to loosen the assertion to check that the callstack is good enough and the test is not flaky.
        // TODO: We will revisit it when ManagedCodeCache is better or the unwinder is better.
        // For now, when asserting the stacktrace on ARM64, we check that:
        // - The callstack is only one frame (alternateExpectedStack)
        // - or the expectedStack is a subset of the actualStack (sliding window comparison)
        private static (bool Matched, string Message) AssertExpectedStack_Arm64(StackTrace actualStack, StackTrace expectedStack)
        {
            var unknownFrameType = "|lm:Unknown-Assembly |ns: |ct:Unknown-Type |cg: |fn:Unknown-Frame-Type |fg: |sg:(?)";

            if (actualStack.FramesCount == 1 && actualStack[0].ToString() == unknownFrameType)
            {
                return (true, string.Empty);
            }

            if (actualStack.FramesCount < expectedStack.FramesCount)
            {
                return (false, $"Actual stack has less frames than expected: {actualStack.FramesCount} < {expectedStack.FramesCount}");
            }

            int lastStart = actualStack.FramesCount - expectedStack.FramesCount;
            bool matched = false;
            for (int start = 0; start <= lastStart; start++)
            {
                matched = true;

                for (int j = 0; j < expectedStack.FramesCount; j++)
                {
                    if (actualStack[start + j].ToString() != expectedStack[j].ToString())
                    {
                        matched = false;
                        break;
                    }
                }
                if (matched)
                {
                    break;
                }
            }

            return (matched,
                    "ARM64 stacktrace did not match either:\n" +
                    "- a single-frame 'Unknown-Frame-Type' placeholder stack, or\n" +
                    "- the expected frames as a contiguous subsequence of the actual stack.\n\n" +
                    $"Expected ({expectedStack.FramesCount} frames):\n{expectedStack}\n\n" +
                    $"Actual ({actualStack.FramesCount} frames):\n{actualStack}");
        }

        private static (bool Matched, string Message) AssertExpectedStack(StackTrace actualStack, StackTrace expectedStack)
        {
            if (EnvironmentHelper.GetPlatform() == "ARM64")
            {
                return AssertExpectedStack_Arm64(actualStack, expectedStack);
            }

            return AssertExpectedStack_x86_64(actualStack, expectedStack);
        }

        /// <summary>
        /// ARM64 + timestamps: samples have per-profile timestamps so rows are not aggregated —
        /// expect exactly the unaggregated multiset (each row count 1), with relaxed stacks.
        /// </summary>
        private static void AssertExceptionSamplesArm64TimestampedMultiset(
            IReadOnlyList<(string Type, string Message, long Count, StackTrace Stacktrace)> actual,
            IReadOnlyList<(string Type, string Message, long Count, StackTrace ExpectedStack)> expected)
        {
            actual.Should().HaveCount(expected.Count);
            actual.Sum(a => a.Count).Should().Be(expected.Sum(e => e.Count));

            var pending = actual.ToList();
            foreach (var exp in expected)
            {
                var idx = pending.FindIndex(
                    s => s.Type == exp.Type && s.Message == exp.Message && s.Count == exp.Count &&
                         AssertExpectedStack(s.Stacktrace, exp.ExpectedStack).Matched);
                idx.Should().BeGreaterThanOrEqualTo(
                    0,
                    $"Expected a sample for {exp.Type} / {exp.Message} / count {exp.Count} with matching stack. " +
                    $"Remaining: {FormatExceptionSamplesSummary(pending)}");
                pending.RemoveAt(idx);
            }

            pending.Should().BeEmpty($"Unexpected extra profile samples: {FormatExceptionSamplesSummary(pending)}");
        }

        /// <summary>
        /// ARM64 branch for <see cref="GetExceptionSamplesWithoutTimestamp"/> (profiler configured without per-sample timestamps).
        /// This is <strong>not</strong> "expect no aggregation": on x86 that run uses <c>aggregatedProfile</c> (IOE and NSE each total weight 2).
        /// On ARM64 the same totals must hold, but IOE/NSE may appear as one pprof row (<c>Count == 2</c>) or two rows (<c>Count == 1</c> each),
        /// so we match bucket <em>sums</em> instead of a fixed row multiset. Stacks use relaxed ARM64 matching.
        /// </summary>
        private static void AssertExceptionSamplesArm64ForNonTimestampedProfile(
            IReadOnlyList<(string Type, string Message, long Count, StackTrace Stacktrace)> actual,
            StackTrace stack1,
            StackTrace stack2)
        {
            // Totals align with non-ARM64 aggregatedProfile (same combined sample weight per exception), not unaggregatedProfile row count.
            var buckets = new[]
            {
                ("System.InvalidOperationException", "IOE", 2L, stack1),
                ("System.NotSupportedException", "NSE", 2L, stack1),
                ("System.NotImplementedException", "NIE", 1L, stack1),
                ("System.NotImplementedException", "NIE", 1L, stack2),
                ("System.Exception", "E1", 1L, stack1),
                ("System.Exception", "E2", 1L, stack1),
            };

            actual.Sum(a => a.Count).Should().Be(buckets.Sum(b => b.Item3));

            // Eat each logical group out of the multiset of pprof rows. What is left must be empty.
            var pending = actual.ToList();
            foreach (var (type, message, totalNeeded, expectedStack) in buckets)
            {
                TryRemovePprofRowsThatCoverBucketTotal(pending, type, message, expectedStack, totalNeeded).Should().BeTrue(
                    $"Could not account for total sample count {totalNeeded} for {type} / {message} with expected stack. " +
                    $"Remaining: {FormatExceptionSamplesSummary(pending)}");
            }

            pending.Should().BeEmpty($"Unexpected leftover profile samples after bucket matching: {FormatExceptionSamplesSummary(pending)}");
        }

        private static string FormatExceptionSamplesSummary(IEnumerable<(string Type, string Message, long Count, StackTrace Stacktrace)> samples) =>
            string.Join("; ", samples.Select(s => $"{s.Type}/{s.Message} count={s.Count}"));

        /// <summary>
        /// Implements one step of
        /// <see cref="AssertExceptionSamplesArm64ForNonTimestampedProfile"/>:
        /// remove from <paramref name="remainingSamples"/> some rows that belong to this bucket
        /// (same exception type + message + stack under <see cref="AssertExpectedStack"/>)
        /// whose per-row pprof <c>Value</c> counts add up to <paramref name="requiredTotalCount"/>.
        /// IOE/NSE typically need counts summing to 2 — either one row with Count=2 or two rows with Count=1.
        /// </summary>
        private static bool TryRemovePprofRowsThatCoverBucketTotal(
            List<(string Type, string Message, long Count, StackTrace Stacktrace)> remainingSamples,
            string expectedExceptionType,
            string expectedMessage,
            StackTrace expectedCallstackShape,
            long requiredTotalCount)
        {
            var indicesOfMatchingRows = new List<int>();
            for (var i = 0; i < remainingSamples.Count; i++)
            {
                var row = remainingSamples[i];
                if (row.Type != expectedExceptionType || row.Message != expectedMessage)
                {
                    continue;
                }

                if (!AssertExpectedStack(row.Stacktrace, expectedCallstackShape).Matched)
                {
                    continue;
                }

                indicesOfMatchingRows.Add(i);
            }

            var counts = indicesOfMatchingRows.Select(i => remainingSamples[i].Count).ToList();
            if (!TryPickIndicesWhoseCountsSumTo(indicesOfMatchingRows, counts, requiredTotalCount, out var indicesToRemove))
            {
                return false;
            }

            foreach (var idx in indicesToRemove.OrderByDescending(i => i))
            {
                remainingSamples.RemoveAt(idx);
            }

            return true;
        }

        /// <summary>
        /// Picks whole pprof rows whose <c>Count</c> values add up to <paramref name="targetSum"/>.
        /// This test only uses bucket totals of <b>1</b> or <b>2</b>, so we only need:
        /// <list type="bullet">
        /// <item><b>1</b> — one row with <c>Count == 1</c></item>
        /// <item><b>2</b> — either one row with <c>Count == 2</c>, or two rows with <c>Count == 1</c> each</item>
        /// </list>
        /// (That covers IOE/NSE “aggregated” vs “split” without subset-sum / bit masks.)
        /// </summary>
        private static bool TryPickIndicesWhoseCountsSumTo(
            IReadOnlyList<int> candidateIndicesIntoPending,
            IReadOnlyList<long> countForEachCandidate,
            long targetSum,
            out List<int> selectedIndicesIntoPending)
        {
            var n = candidateIndicesIntoPending.Count;
            if (n == 0)
            {
                selectedIndicesIntoPending = new List<int>();
                return targetSum == 0;
            }

            switch (targetSum)
            {
                case 1L:
                    return TryPickSingleRowWithExactCount(
                        candidateIndicesIntoPending,
                        countForEachCandidate,
                        exactCount: 1L,
                        out selectedIndicesIntoPending);

                case 2L:
                    if (TryPickSingleRowWithExactCount(
                            candidateIndicesIntoPending,
                            countForEachCandidate,
                            exactCount: 2L,
                            out selectedIndicesIntoPending))
                    {
                        return true;
                    }

                    return TryPickTwoRowsWithCountOne(
                        candidateIndicesIntoPending,
                        countForEachCandidate,
                        out selectedIndicesIntoPending);

                default:
                    throw new InvalidOperationException(
                        $"Unexpected bucket total {targetSum} for ARM64 non-timestamp profile matching (expected 1 or 2).");
            }
        }

        private static bool TryPickSingleRowWithExactCount(
            IReadOnlyList<int> candidateIndicesIntoPending,
            IReadOnlyList<long> countForEachCandidate,
            long exactCount,
            out List<int> selectedIndicesIntoPending)
        {
            for (var i = 0; i < candidateIndicesIntoPending.Count; i++)
            {
                if (countForEachCandidate[i] != exactCount)
                {
                    continue;
                }

                selectedIndicesIntoPending = new List<int> { candidateIndicesIntoPending[i] };
                return true;
            }

            selectedIndicesIntoPending = new List<int>();
            return false;
        }

        private static bool TryPickTwoRowsWithCountOne(
            IReadOnlyList<int> candidateIndicesIntoPending,
            IReadOnlyList<long> countForEachCandidate,
            out List<int> selectedIndicesIntoPending)
        {
            var withCountOne = new List<int>();
            for (var i = 0; i < candidateIndicesIntoPending.Count; i++)
            {
                if (countForEachCandidate[i] != 1L)
                {
                    continue;
                }

                withCountOne.Add(candidateIndicesIntoPending[i]);
                if (withCountOne.Count == 2)
                {
                    selectedIndicesIntoPending = withCountOne;
                    return true;
                }
            }

            selectedIndicesIntoPending = new List<int>();
            return false;
        }

        private void CheckExceptionProfiles(TestApplicationRunner runner, bool withTimestamps)
        {
            // in .NET 9 these stacks are different in the 64-bit case
            // Stack 1
            // |lm:System.Private.CoreLib |ns:System.Runtime |ct:EH |cg: |fn:DispatchEx |fg: |sg:(System.Runtime.StackFrameIterator& frameIter, ExInfo& exInfo)
            // |lm:System.Private.CoreLib |ns:System.Runtime |ct:EH |cg: |fn:RhThrowEx |fg: |sg:(object exceptionObj, ExInfo& exInfo)
            // |lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ExceptionsProfilerTestScenario |cg: |fn:Throw1_2 |fg: |sg:(System.Exception ex)
            // |lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ExceptionsProfilerTestScenario |cg: |fn:Throw1_1 |fg: |sg:(System.Exception ex)
            // |lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ExceptionsProfilerTestScenario |cg: |fn:Throw1 |fg: |sg:(System.Exception ex)
            // |lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ExceptionsProfilerTestScenario |cg: |fn:Run |fg: |sg:()
            // |lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:Program |cg: |fn:Main |fg: |sg:(string[] args).

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

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var exceptionSamples = SamplesHelper.ExtractExceptionSamples(runner.Environment.PprofDir).ToArray();

            var unaggregatedProfile = new List<(string Type, string Message, long Count, StackTrace ExpectedStack)>
            {
                ("System.InvalidOperationException", "IOE", 1, stack1),
                ("System.InvalidOperationException", "IOE", 1, stack1),
                ("System.NotSupportedException", "NSE", 1, stack1),
                ("System.NotSupportedException", "NSE", 1, stack1),
                ("System.NotImplementedException", "NIE", 1, stack1),
                ("System.NotImplementedException", "NIE", 1, stack2),
                ("System.Exception", "E1", 1, stack1),
                ("System.Exception", "E2", 1, stack1)
            };

            if (EnvironmentHelper.GetPlatform() == "ARM64")
            {
                if (withTimestamps)
                {
                    // Rows follow profile time order and are not aggregated — exact multiset of 8 × count 1.
                    AssertExceptionSamplesArm64TimestampedMultiset(exceptionSamples, unaggregatedProfile);
                }
                else
                {
                    // No per-sample timestamps: IOE/NSE may be one aggregated row or two unaggregated rows; stacks stay relaxed.
                    AssertExceptionSamplesArm64ForNonTimestampedProfile(exceptionSamples, stack1, stack2);
                }
            }
            else if (withTimestamps)
            {
                // No aggregation; exact multiset and stack equality.
                exceptionSamples.Should().BeEquivalentTo(unaggregatedProfile);
            }
            else
            {
                // IOE and NSE aggregated into one sample each on other platforms.
                exceptionSamples.Should().BeEquivalentTo(
                    new List<(string Type, string Message, long Count, StackTrace ExpectedStack)>
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
