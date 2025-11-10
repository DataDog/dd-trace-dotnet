// <copyright file="ContentionProfilerTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Contention
{
    public class ContentionProfilerTest
    {
        private const string ScenarioContention = "--scenario 10 --threads 20";
        private const string ScenarioWithoutContention = "--scenario 6 --threads 1";

        private readonly ITestOutputHelper _output;

        public ContentionProfilerTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Flags]
        private enum WaitHandleType
        {
            None = 0,
            AutoResetEvent = 1,
            ManualResetEvent = 2,
            ManualResetEventSlim = 4,
            Mutex = 8,
            Semaphore = 16,
            SemaphoreSlim = 32,
            ReaderWriterLock = 64,
            ReaderWriterLockSlim = 128,

            All = AutoResetEvent | ManualResetEvent | ManualResetEventSlim | Mutex | Semaphore | SemaphoreSlim | ReaderWriterLock | ReaderWriterLockSlim
        }

        // FIXME: .NET 10 skipping .NET 10 for now as ReaderWriterLockSlim is missing for some reason on Linux
        [TestAppFact("Samples.WaitHandles", new[] { "net9.0" })]
        public void ShouldGetWaitSamples(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--iterations 1");
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.ContentionProfilerEnabled, "1");
            // BUG: uncomment these lines to investigate missing root frame in contention but present in exception samples
            // runner.Environment.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "1");
            // runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.WaitHandleContentionProfilingEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            // only contention profiler enabled so should only see the 2 contention related values per sample
            SamplesHelper.CheckSamplesValueCount(runner.Environment.PprofDir, 2);
            Assert.True(SamplesHelper.IsLabelPresent(runner.Environment.PprofDir, "raw duration"));

            AssertContainWait(runner.Environment.PprofDir);
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net7.0", "net8.0", "net10.0" })]
        public void ShouldGetContentionSamples(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioContention);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.ContentionProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.ThreadLifetimeEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            // contention and thread lifetime profilers enabled so should see 3 values per sample
            SamplesHelper.CheckSamplesValueCount(runner.Environment.PprofDir, 3);
            Assert.True(SamplesHelper.IsLabelPresent(runner.Environment.PprofDir, "raw duration"));

            if (framework == "net8.0")
            {
                AssertBlockingThreadLabel(runner.Environment.PprofDir);
            }
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net7.0", "net8.0", "net10.0" })]
        public void ShouldContentionProfilerBeEnabledByDefault(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioContention);

            // disabled all default profiles except contention and wall time
            // thread lifetime profiler enabled <= this one will help us gathering
            // an accurate view on the threads that are alive during the profiling
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.GarbageCollectionProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.GcThreadsCpuTimeEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.ThreadLifetimeEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            // contention and thread lifetime profilers enabled so should see 3 values per sample
            SamplesHelper.CheckSamplesValueCount(runner.Environment.PprofDir, 3);
            Assert.NotEqual(0, SamplesHelper.GetSamplesCount(runner.Environment.PprofDir));

            if (framework == "net8.0")
            {
                AssertBlockingThreadLabel(runner.Environment.PprofDir);
            }
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net7.0", "net8.0", "net10.0" })]
        public void ExplicitlyDisableContentionProfiler(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioContention);

            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            // only walltime profiler enabled so should see 1 value per sample
            SamplesHelper.CheckSamplesValueCount(runner.Environment.PprofDir, 1);
        }

        [TestAppFact("Samples.Computer01", new[] { "net48" })]
        public void ShouldGetLockContentionSamplesViaEtw(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioWithoutContention);
            // allow agent proxy to send the recorded events
            runner.TestDurationInSeconds = 30;

            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.ContentionProfilerEnabled, "1");
            Guid guid = Guid.NewGuid();
            runner.Environment.SetVariable(EnvironmentVariables.EtwReplayEndpoint, "\\\\.\\pipe\\DD_ETW_TEST_AGENT-" + guid);

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            if (IntPtr.Size == 4)
            {
                // 32-bit
                agent.StartEtwProxy(runner.XUnitLogger, "DD_ETW_TEST_AGENT-" + guid, "Contention\\lockContention-32.bevents");
            }
            else
            {
                // 64-bit
                agent.StartEtwProxy(runner.XUnitLogger, "DD_ETW_TEST_AGENT-" + guid, "Contention\\lockContention-64.bevents");
            }

            int eventsCount = 0;
            agent.EventsSent += (sender, e) => eventsCount = e.Value;

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);
            AssertContainLockContentionSamples(runner.Environment.PprofDir);
            Assert.True(eventsCount > 0);
        }

        private static void AssertContainLockContentionSamples(string pprofDir)
        {
            var contentionSamples = SamplesHelper.GetSamples(pprofDir, "lock-count");
            contentionSamples.Should().NotBeEmpty();
        }

        private static void AssertBlockingThreadLabel(string pprofDir)
        {
            var threadIds = SamplesHelper.GetThreadIds(pprofDir);
            // get samples with lock-count value set and blocking thread info
            var contentionSamples = SamplesHelper.GetSamples(pprofDir, "lock-count")
                .Where(e => e.Labels.Any(x => x.Name == "blocking thread id"));

            contentionSamples.Should().NotBeEmpty();

            foreach (var (_, labels, _) in contentionSamples)
            {
                var blockingThreadIdLabel = labels.FirstOrDefault(l => l.Name == "blocking thread id");
                blockingThreadIdLabel.Name.Should().NotBeNullOrWhiteSpace();
                threadIds.Should().Contain(int.Parse(blockingThreadIdLabel.Value), $"Unknown blocking thread id {blockingThreadIdLabel.Value}");

                var blockingThreadNameLabel = labels.FirstOrDefault(l => l.Name == "blocking thread name");
                blockingThreadIdLabel.Name.Should().NotBeNullOrWhiteSpace();
                blockingThreadIdLabel.Value.Should().NotBeNullOrWhiteSpace();
            }
        }

        private static void AssertContainWait(string pprofDir)
        {
            // get samples with lock-count value set and Wait as contention type
            var waitSamples = SamplesHelper.GetSamples(pprofDir, "lock-count")
                .Where(e => e.Labels.Any(x => (x.Name == "contention type") && (x.Value == "Wait")));
            Assert.True(waitSamples.Count() > 8);

            // check that we get samples 500+ ms for supported wait handles based on callstacks
            // |lm:Samples.WaitHandles |ns:Samples.WaitHandles |ct:Program |cg: |fn:AutoResetEventThread |fg: |sg:()
            // |lm:Samples.WaitHandles |ns:Samples.WaitHandles |ct:Program |cg: |fn:ReaderWriterLockSlimThread |fg: |sg:()
            // |lm:Samples.WaitHandles |ns:Samples.WaitHandles |ct:Program |cg: |fn:SemaphoreSlimThread |fg: |sg:()
            // |lm:Samples.WaitHandles |ns:Samples.WaitHandles |ct:Program |cg: |fn:ManualResetEventSlimThread |fg: |sg:()
            // |lm:Samples.WaitHandles |ns:Samples.WaitHandles |ct:Program |cg: |fn:ManualResetEventThread |fg: |sg:()
            // |lm:Samples.WaitHandles |ns:Samples.WaitHandles |ct:Program |cg: |fn:ReaderWriterLockThread |fg: |sg:()
            // |lm:Samples.WaitHandles |ns:Samples.WaitHandles |ct:Program |cg: |fn:SemaphoreThread |fg: |sg:()
            // |lm:Samples.WaitHandles |ns:Samples.WaitHandles |ct:Program |cg: |fn:MutexThread |fg: |sg:()
            // Duration bucket = +500 ms

            WaitHandleType waitTypes = WaitHandleType.None;
            foreach (var (stackTrace, labels, _) in waitSamples
                .Where(s => s.Labels.Any(l => (l.Value == "+500 ms") && (l.Name == "Duration bucket"))))
            {
                var contentionTypeLabel = labels.FirstOrDefault(l => l.Name == "contention type");
                stackTrace.FramesCount.Should().BeGreaterThan(0);

                for (int currentFrame = 0; currentFrame < stackTrace.FramesCount; currentFrame++)
                {
                    var function = stackTrace[currentFrame].Function;
                    if (function.Contains("AutoResetEventThread"))
                    {
                        waitTypes |= WaitHandleType.AutoResetEvent;
                    }
                    else if (function.Contains("ManualResetEventThread"))
                    {
                        waitTypes |= WaitHandleType.ManualResetEvent;
                    }
                    else if (function.Contains("SemaphoreThread"))
                    {
                        waitTypes |= WaitHandleType.Semaphore;
                    }
                    else if (function.Contains("ReaderWriterLockThread"))
                    {
                        waitTypes |= WaitHandleType.ReaderWriterLock;
                    }
                    else if (function.Contains("ReaderWriterLockSlimThread"))
                    {
                        waitTypes |= WaitHandleType.ReaderWriterLockSlim;
                    }
                    else if (function.Contains("MutexThread"))
                    {
                        waitTypes |= WaitHandleType.Mutex;
                    }
                    else
                    {
                        var type = stackTrace[currentFrame].Type;

                        // BUG: should see ManualResetEventSlimThread as root frame but absent on Linux
                        if (type == "ManualResetEventSlim")
                        {
                            waitTypes |= WaitHandleType.ManualResetEventSlim;
                        }

                        // BUG: should see SemaphoreSlimThread as root frame but absent on Linux
                        else if (type == "SemaphoreSlim")
                        {
                            waitTypes |= WaitHandleType.SemaphoreSlim;
                        }
                    }
                }

                // check that we also have the wait label
                var waitDurationLabel = labels.FirstOrDefault(l => l.Name == "Wait duration bucket");
                waitDurationLabel.Name.Should().NotBeNull();
            }

            waitTypes.Should().Be(WaitHandleType.All, "missing Wait events = " + GetMissingWaitType(waitTypes));

            // check that wait duration label should not be present for lock samples
            var lockSamples = SamplesHelper.GetSamples(pprofDir, "lock-count")
                .Where(e => e.Labels.Any(x => (x.Name == "contention type") && (x.Value == "Lock")));
            foreach (var (_, labels, _) in lockSamples)
            {
                var waitDurationLabel = labels.FirstOrDefault(l => l.Name == "Wait duration bucket");
                waitDurationLabel.Name.Should().BeNull();
            }
        }

        private static string GetMissingWaitType(WaitHandleType waitTypes)
        {
            string missingWaitTypes = string.Empty;
            if (!waitTypes.HasFlag(WaitHandleType.AutoResetEvent))
            {
                missingWaitTypes += "AutoResetEvent ";
            }

            if (!waitTypes.HasFlag(WaitHandleType.ManualResetEvent))
            {
                missingWaitTypes += "ManualResetEvent ";
            }

            if (!waitTypes.HasFlag(WaitHandleType.ManualResetEventSlim))
            {
                missingWaitTypes += "ManualResetEventSlim ";
            }

            if (!waitTypes.HasFlag(WaitHandleType.Semaphore))
            {
                missingWaitTypes += "Semaphore ";
            }

            if (!waitTypes.HasFlag(WaitHandleType.SemaphoreSlim))
            {
                missingWaitTypes += "SemaphoreSlim ";
            }

            if (!waitTypes.HasFlag(WaitHandleType.ReaderWriterLock))
            {
                missingWaitTypes += "ReaderWriterLock ";
            }

            if (!waitTypes.HasFlag(WaitHandleType.ReaderWriterLockSlim))
            {
                missingWaitTypes += "ReaderWriterLockSlim ";
            }

            if (!waitTypes.HasFlag(WaitHandleType.Mutex))
            {
                missingWaitTypes += "Mutex ";
            }

            if (missingWaitTypes == string.Empty)
            {
                missingWaitTypes = "All";
            }

            return "{ " + missingWaitTypes + "}";
        }
    }
}
