// <copyright file="AppDomainCrashTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.WindowsOnly
{
    [Trait("Category", "WindowsOnly")]
    public class AppDomainCrashTest
    {
        private readonly ITestOutputHelper _output;

        public AppDomainCrashTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01", new[] { "net48" })]
        public void CheckNoCrashWhenThreadGetsReassignToAnotherAppDomain(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, commandLine: "--scenario 30 --param 5", output: _output);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            SamplesHelper.GetSamples(runner.Environment.PprofDir)
                .Where(x => x.Labels.Any(y => y.Name == "exception type" && y.Value == "CatException"))
                .All(x => x.Labels.Any(y => y.Name == "appdomain name" && y.Value == "Test-1"))
                .Should().BeTrue();
        }
    }
}
