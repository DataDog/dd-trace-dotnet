// <copyright file="SignalHandlerTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

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

        [TestAppFact(TestApplication.Computer01, TestFramework.Net6)]
        public void CheckApplicationWithItsOwnSignalHandler(TestAppContext app)
        {
            var runner = new SmokeTestRunner(app, ScenarioLinuxHandler, _output);
            runner.RunAndCheck();
        }

        [TestAppFact(TestApplication.ExceptionGenerator)]
        public void CheckSignalHandlerIsInstalledOnce(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, ScenarioLinuxHandler);

            runner.Environment.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "1");

            using var run = runner.Run();

            run.UploadCount.Should().BeGreaterThan(0);

            run.Logs.NativeProfiler
                .LinesContaining("Successfully setup signal handler for User defined signal 1 signal")
                .Should().HaveCount(1);

            run.Logs.NativeProfiler
                .LinesContaining("Successfully setup signal handler for Profiling timer expired signal")
                .Should().HaveCount(1);
        }
    }
}
