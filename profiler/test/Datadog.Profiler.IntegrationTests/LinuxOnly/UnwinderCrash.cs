// <copyright file="UnwinderCrash.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.SmokeTests;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.LinuxOnly
{
    [Trait("Category", "LinuxOnly")]
    public class UnwinderCrash
    {
        private const string Scenario = "--scenario 23";
        private readonly ITestOutputHelper _output;

        public UnwinderCrash(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckThatProfilerDoesNotCrashWhileUnwinding2SignalFrames(string appName, string framework, string appAssembly)
        {
            var runner = new SmokeTestRunner(appName, framework, appAssembly, Scenario, _output)
            {
                // this can be flaky time to time
                // We just want to make sure that at least one pprof file was written to disk
                // and the process did not crash.
                MinimumExpectedNbPprofFiles = 1
            };
            runner.RunAndCheck();
        }
    }
}
