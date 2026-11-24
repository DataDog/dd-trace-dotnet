// <copyright file="NullOrEmptyThreadNameCheck.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.IntegrationTests.Helpers.Profiles;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Bugs
{
    public class NullOrEmptyThreadNameCheck
    {
        private const string ScenarioNullOrEmptyThreadName = "--scenario 19";

        private readonly ITestOutputHelper _output;

        public NullOrEmptyThreadNameCheck(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void ShouldNotCrashWhenNullOrEmptyThreadName(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: ScenarioNullOrEmptyThreadName);
            // only CPU is disabled: the other default profilers stay on
            runner.Environment.EnableOnly(
                ProfilerFeature.WallTime,
                ProfilerFeature.Exceptions,
                ProfilerFeature.LockContention,
                ProfilerFeature.GarbageCollection,
                ProfilerFeature.GcThreadsCpuTime,
                ProfilerFeature.Timeline);

            using var run = runner.Run();

            // we should not see any crash + get callstacks for threads with no name
            bool threadWithNameFound = false;
            foreach (var sample in run.Profiles.Samples)
            {
                var threadName = sample.Labels.GetValues(ProfileLabelNames.ThreadName).FirstOrDefault();

                if (threadName == string.Empty)
                {
                    if (sample.CallStack.FramesCount == 0)
                    {
                        Assert.Fail("No call stack for thread without name");
                        return;
                    }
                }
                else if (threadName.StartsWith(".NET "))
                {
                    // .NET ThreadPool IO, .NET TP Worker, .NET Long Running Task, .NET Timer, .NET TP Wait, etc...
                }
                else if (threadName == "CLR thread (garbage collector)")
                {
                    // hard coded name for GC threads
                }
                else
                {
                    threadWithNameFound = true;
                }
            }

            Assert.False(threadWithNameFound);
        }
    }
}
