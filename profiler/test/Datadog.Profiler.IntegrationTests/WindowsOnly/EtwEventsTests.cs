// <copyright file="EtwEventsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.IntegrationTests.Helpers.Logs;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.WindowsOnly
{
    [Trait("Category", "WindowsOnly")]

    public class EtwEventsTests
    {
        private readonly ITestOutputHelper _output;

        public EtwEventsTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48)]
        public void CheckErrorWhenNoAgentIsAvailable(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            ProfilerLogSet.Read(runner.Environment.LogDir).NativeProfiler
                .Should().ContainLine("Impossible to connect to the Datadog Agent named pipe");
        }
    }
}
