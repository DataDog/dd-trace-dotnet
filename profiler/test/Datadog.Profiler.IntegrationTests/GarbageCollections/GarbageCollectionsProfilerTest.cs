// <copyright file="GarbageCollectionsProfilerTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.IntegrationTests.Helpers.Profiles;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.GarbageCollections
{
    public class GarbageCollectionsProfilerTest
    {
        private const string ScenarioGenerics = "--scenario 12";
        private const string ScenarioWithoutGC = "--scenario 6 --threads 1";
        private const string GcRootFrame = "|lm: |ns: |ct: |cg: |fn:Garbage Collector |fg: |sg:";

        private readonly ITestOutputHelper _output;

        public GarbageCollectionsProfilerTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net6, TestFramework.Net7)]
        public void ShouldGetGarbageCollectionSamples(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: ScenarioGenerics);
            runner.Environment.EnableOnly(ProfilerFeature.GarbageCollection);

            // only garbage collection profiler enabled so should only see the 1 related value per sample + Generation label
            using var run = runner.Run();
            Assert.True(run.UploadCount > 0);
            Assert.True(CheckSamplesAreGC(run.Profiles));
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net6, TestFramework.Net7)]
        public void ShouldGetGarbageCollectionSamplesByDefault(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: ScenarioGenerics);

            runner.Environment.DisableAllExcept(ProfilerFeature.GarbageCollection);

            // only garbage collection profiler enabled so should only see the 1 related value per sample + Generation label
            using var run = runner.Run();
            Assert.True(run.UploadCount > 0);
            Assert.True(CheckSamplesAreGC(run.Profiles));
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48)]
        public void ShouldGetGarbageCollectionSamplesViaEtw(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: ScenarioWithoutGC);
            // allow agent proxy to send the recorded events
            runner.TestDurationInSeconds = 30;

            runner.Environment.DisableAllExcept(ProfilerFeature.GarbageCollection);
            Guid guid = Guid.NewGuid();
            runner.Environment.SetVariable(EnvironmentVariables.EtwReplayEndpoint, "\\\\.\\pipe\\DD_ETW_TEST_AGENT-" + guid);

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            if (IntPtr.Size == 4)
            {
                // 32-bit
                agent.StartEtwProxy(runner.XUnitLogger, "DD_ETW_TEST_AGENT-" + guid, "GarbageCollections\\3x3GCs-32.bevents");
            }
            else
            {
                // 64-bit
                agent.StartEtwProxy(runner.XUnitLogger, "DD_ETW_TEST_AGENT-" + guid, "GarbageCollections\\3x3GCs-64.bevents");
            }

            int eventsCount = 0;
            agent.EventsSent += (sender, e) => eventsCount = e.Value;
            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);
            Assert.True(eventsCount > 0);
            Assert.True(CheckSamplesAreGC(ProfileReader.Read(runner.Environment.PprofDir)));
            // TODO: it should be possible to ensure that 3 GCs per generations are seen in the .pprof files
        }

        private static bool CheckSamplesAreGC(ProfileOutput profiles)
        {
            var rootFrame = new StackFrame(GcRootFrame);

            return profiles.AllSamplesMatch(sample => IsGcSample(sample, rootFrame));
        }

        private static bool IsGcSample(ProfileSample sample, StackFrame rootFrame)
        {
            if (!sample.Labels.Has("gc generation"))
            {
                return false;
            }

            // check fake call stack
            var frames = sample.CallStack;
            if (frames.FramesCount != 2)
            {
                return false;
            }

            if (!frames[0].Equals(rootFrame))
            {
                return false;
            }

            var generation = frames[1].Function;
            if (!generation.StartsWith("gen"))
            {
                return false;
            }

            return generation.EndsWith("0") || generation.EndsWith("1") || generation.EndsWith("2");
        }
    }
}
