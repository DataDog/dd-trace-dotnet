// <copyright file="SignalHandlerTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.IO;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.SmokeTests;
using FluentAssertions;
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

        [TestAppFact("Samples.ExceptionGenerator")]
        public void CheckSignalHandlerIsInstalledOnce(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, ScenarioLinuxHandler);

            runner.Environment.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
                .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            var nbSignalHandlerInstallation = File.ReadLines(logFile)
                .Count(l => l.Contains("Successfully setup signal handler for"));

            nbSignalHandlerInstallation.Should().Be(1);
        }
    }
}
