// <copyright file="CpuLimitTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.SmokeTests;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.SmokeTests
{
    public class CpuLimitTest
    {
        private readonly ITestOutputHelper _output;

        public CpuLimitTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Trait("Category", "CpuLimitTest")]
        [TestAppFact("Samples.BuggyBits")]
        public void CheckCpuLimit(string appName, string framework, string appAssembly)
        {
            var runner = new SmokeTestRunner(appName, framework, appAssembly, commandLine: "--scenario 1", output: _output);

            using var agent = runner.Run();

            var rawCpuLimit = Environment.GetEnvironmentVariable("CONTAINER_CPUS");

            if (double.TryParse(rawCpuLimit, out var cpuLimit) && cpuLimit < 1)
            {
                _output.WriteLine("CPU limit set to <1 CPU, expecting profiler to be disabled");
                agent.NbCallsOnProfilingEndpoint.Should().Be(0);
            }
            else
            {
                _output.WriteLine("CPU limit set to >=1 CPU or not set, expecting profiler to be enabled");
                agent.NbCallsOnProfilingEndpoint.Should().BeGreaterThan(0);
            }
        }
    }
}
