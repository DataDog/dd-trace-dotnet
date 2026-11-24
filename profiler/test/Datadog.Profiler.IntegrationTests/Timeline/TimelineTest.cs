// <copyright file="TimelineTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.IntegrationTests.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Timeline
{
    public class TimelineTest
    {
        private readonly ITestOutputHelper _output;

        public TimelineTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Flaky("Samples.BuggyBits intermittently exits with code 255 during profiling")]
        [TestAppFact(TestApplication.BuggyBits)]
        public void CheckTimestampAsLabel(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 5");
            // GC threads CPU time and thread lifetime are enabled by default
            // TODO: add any new profiler to ensure that all are setting timestamps as label
            runner.Environment.EnableOnly(
                ProfilerFeature.WallTime,
                ProfilerFeature.Cpu,
                ProfilerFeature.Exceptions,
                ProfilerFeature.Allocations,
                ProfilerFeature.LockContention,
                ProfilerFeature.LiveHeap,
                ProfilerFeature.GarbageCollection,
                ProfilerFeature.GcThreadsCpuTime,
                ProfilerFeature.Timeline,
                ProfilerFeature.TimestampsAsLabel);

            using var run = runner.Run();

            Assert.True(run.UploadCount > 0);
            // only the GC CPU sample does not have a timestamp label
            Assert.True(run.Profiles.Samples.All(sample => sample.Labels.Has("end_timestamp_ns") || sample.Labels.Has("gc_cpu_sample")));
        }
    }
}
