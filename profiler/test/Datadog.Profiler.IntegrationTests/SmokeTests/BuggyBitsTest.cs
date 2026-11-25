// <copyright file="BuggyBitsTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Datadog.Profiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.SmokeTests
{
    public class BuggyBitsTest
    {
        private readonly ITestOutputHelper _output;

        public BuggyBitsTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact(TestApplication.BuggyBits)]
        public void CheckSmoke(TestAppContext app)
        {
            var runner = new SmokeTestRunner(app, commandLine: "--scenario 1", output: _output);
            runner.RunAndCheck();
        }

        [Trait("Category", "LinuxOnly")]
        [TestAppFact(TestApplication.BuggyBits)]
        public void CheckSmokeForNewCpuProfiler(TestAppContext app)
        {
            var runner = new SmokeTestRunner(app, commandLine: "--scenario 1", output: _output);
            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.CpuProfilerType, "TimerCreate");
            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");
            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.MemoryFootprintEnabled, "1");
            runner.RunAndCheck();

            CpuProfilerHelper.SkipIfTimerCreateWasDowngraded(runner.EnvironmentHelper.LogDir);
        }
    }
}
