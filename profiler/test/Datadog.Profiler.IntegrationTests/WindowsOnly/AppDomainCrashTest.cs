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

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48)]
        public void CheckNoCrashWhenThreadGetsReassignToAnotherAppDomain(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 30 --param 5");
            runner.Environment.EnableOnly(ProfilerFeature.Exceptions);

            using var run = runner.Run();

            run.Profiles.WithLabel("exception type", "CatException")
                .Should().NotContain(sample => !sample.Labels.GetValues("appdomain name").Contains("Test-1"));
        }
    }
}
