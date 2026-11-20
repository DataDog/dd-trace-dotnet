// <copyright file="ContentionProfilerTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.IntegrationTests.Helpers.Profiles;
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
        [TestAppFact(TestApplication.WaitHandles, TestFramework.Net9)]
        public void ShouldGetWaitSamples(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--iterations 1");
            // BUG: add ProfilerFeature.Exceptions and ProfilerFeature.WallTime to investigate missing root frame in contention but present in exception samples
            runner.Environment.EnableOnly(ProfilerFeature.LockContention, ProfilerFeature.WaitHandleContention);

            using var run = runner.Run();

            run.Profiles.Should().HaveSampleTypes(ProfileValue.LockCount, ProfileValue.LockTime);
            Assert.True(run.Profiles.Samples.All(sample => sample.Labels.Has("raw duration")));

            AssertContainWait(run.Profiles);
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net6, TestFramework.Net7, TestFramework.Net8, TestFramework.Net10, TestFramework.Net11)]
        public void ShouldGetContentionSamples(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: ScenarioContention);
            runner.Environment.EnableOnly(ProfilerFeature.LockContention);

            using var run = runner.Run();

            run.Profiles.Should().HaveSampleTypes(ProfileValue.LockCount, ProfileValue.LockTime);
            Assert.True(run.Profiles.Samples.All(sample => sample.Labels.Has("raw duration")));

            if (app.Framework == TestFramework.Net8)
            {
                AssertBlockingThreadLabel(run.Profiles);
            }
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net6, TestFramework.Net7, TestFramework.Net8, TestFramework.Net10, TestFramework.Net11)]
        public void ShouldContentionProfilerBeEnabledByDefault(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: ScenarioContention);
            runner.Environment.DisableAllExcept(ProfilerFeature.LockContention);

            using var run = runner.Run();

            run.Profiles.Should().HaveSampleTypes(ProfileValue.LockCount, ProfileValue.LockTime);
            Assert.NotEmpty(run.Profiles.Samples);

            if (app.Framework == TestFramework.Net8)
            {
                AssertBlockingThreadLabel(run.Profiles);
            }
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net6, TestFramework.Net7, TestFramework.Net8, TestFramework.Net10, TestFramework.Net11)]
        public void ExplicitlyDisableContentionProfiler(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: ScenarioContention);
            runner.Environment.EnableOnly(ProfilerFeature.WallTime);

            using var run = runner.Run();

            run.Profiles.Should().HaveSampleTypes(ProfileValue.WallTime);
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48)]
        public void ShouldGetLockContentionSamplesViaEtw(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: ScenarioWithoutContention);
            // allow agent proxy to send the recorded events
            runner.TestDurationInSeconds = 30;

            runner.Environment.EnableOnly(ProfilerFeature.LockContention);
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
            ProfileReader.Read(runner.Environment.PprofDir).WithNonZeroValue(ProfileValue.LockCount).Should().NotBeEmpty();
            Assert.True(eventsCount > 0);
        }

        private static void AssertBlockingThreadLabel(ProfileOutput profiles)
        {
            var threadIds = profiles.Samples.DistinctLabelValues(ProfileLabelNames.ThreadId).Select(int.Parse).ToList();
            // get samples with lock-count value set and blocking thread info
            var contentionSamples = profiles.WithNonZeroValue(ProfileValue.LockCount)
                .Where(sample => sample.Labels.Has(ProfileLabelNames.BlockingThreadId))
                .ToList();

            contentionSamples.Should().NotBeEmpty();

            foreach (var sample in contentionSamples)
            {
                var blockingThreadId = sample.Labels.GetRequired(ProfileLabelNames.BlockingThreadId);
                blockingThreadId.Should().NotBeNullOrWhiteSpace();
                threadIds.Should().Contain(int.Parse(blockingThreadId), $"Unknown blocking thread id {blockingThreadId}");
            }
        }

        private static void AssertContainWait(ProfileOutput profiles)
        {
            // get samples with lock-count value set and Wait as contention type
            var lockCountSamples = profiles.WithNonZeroValue(ProfileValue.LockCount);
            var waitSamples = lockCountSamples.WithLabel(ProfileLabelNames.ContentionType, "Wait");
            Assert.True(waitSamples.Count > 8);

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
            foreach (var sample in waitSamples.WithLabel("Duration bucket", "+500 ms"))
            {
                var stackTrace = sample.CallStack;
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
                sample.Labels.Has("Wait duration bucket").Should().BeTrue();
            }

            waitTypes.Should().Be(WaitHandleType.All, "missing Wait events = " + GetMissingWaitType(waitTypes));

            // check that wait duration label should not be present for lock samples
            foreach (var sample in lockCountSamples.WithLabel(ProfileLabelNames.ContentionType, "Lock"))
            {
                sample.Labels.Has("Wait duration bucket").Should().BeFalse();
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
