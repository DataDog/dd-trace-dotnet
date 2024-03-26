// <copyright file="NullOrEmptyThreadNameCheck.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
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

        [TestAppFact("Samples.Computer01")]
        public void ShouldNotCrashWhenNullOrEmptyThreadName(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioNullOrEmptyThreadName);
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            // we should not see any crash + get callstacks for threads with no name
            bool threadWithNameFound = false;
            foreach (var profile in SamplesHelper.GetProfiles(runner.Environment.PprofDir))
            {
                foreach (var sample in profile.Sample)
                {
                    var labels = sample.Labels(profile).ToArray();
                    var threadName = labels.FirstOrDefault((l) =>
                    {
                        return (l.Name == "thread name");
                    }).Value;

                    if (threadName.Contains("Managed thread (name unknown) ["))
                    {
                        var stackTrace = sample.StackTrace(profile);
                        if (stackTrace.FramesCount == 0)
                        {
                            Assert.Fail("No call stack for thread without name");
                            return;
                        }
                    }
                    else if (threadName.Contains(".NET Long Running Task ["))
                    {
                        // expected task that is waiting for nameless threads to join
                    }
                    else
                    {
                        threadWithNameFound = true;
                    }
                }
            }

            Assert.False(threadWithNameFound);
        }
    }
}
