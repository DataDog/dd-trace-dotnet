// <copyright file="MallocDeadlock.cs" company="Datadog">
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
    public class MallocDeadlock
    {
        private const string ScenarioLinuxMallocDeadlock = "--scenario 15";
        private readonly ITestOutputHelper _output;

        public MallocDeadlock(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckApplicationDoesNotEndUpInDeadlock(string appName, string framework, string appAssembly)
        {
            var runner = new SmokeTestRunner(appName, framework, appAssembly, ScenarioLinuxMallocDeadlock, _output);
            runner.RunAndCheck();
        }
    }
}
