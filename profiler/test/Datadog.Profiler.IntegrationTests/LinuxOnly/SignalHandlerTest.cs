// <copyright file="SignalHandlerTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Datadog.Profiler.SmokeTests;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.LinuxOnly
{
    [Trait("Category", "LinuxOnly")]
    public class SignalHandlerTest
    {
        private const string ScenarioLinuxHandler = "--scenario 11";
        private readonly ITestOutputHelper _output;

        public SignalHandlerTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01", frameworks: new[] { "net6.0" })]
        public void CheckApplicationWithItsOwnSignalHandler(string appName, string framework, string appAssembly)
        {
            var runner = new SmokeTestRunner(appName, framework, appAssembly, ScenarioLinuxHandler, _output);
            runner.RunAndCheck();
        }
    }
}
