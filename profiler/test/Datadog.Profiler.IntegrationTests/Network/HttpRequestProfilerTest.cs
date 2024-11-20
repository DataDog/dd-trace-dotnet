// <copyright file="HttpRequestProfilerTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Datadog.Profiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Network
{
    public class HttpRequestProfilerTest
    {
        private const string Iterations = "7";

        private readonly ITestOutputHelper _output;

        public HttpRequestProfilerTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.ParallelCountSites", new[] { "net6.0", "net7.0", "net8.0" })]
        public void ShouldGetHttpSamples(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Iterations);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.HttpProfilingEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            // only network profiler enabled so should only see one value per sample
            SamplesHelper.CheckSamplesValueCount(runner.Environment.PprofDir, 1);
            Assert.True(SamplesHelper.IsLabelPresent(runner.Environment.PprofDir, "request url"));
            Assert.True(
                SamplesHelper.IsLabelPresent(runner.Environment.PprofDir, "request status code") ||
                SamplesHelper.IsLabelPresent(runner.Environment.PprofDir, "request error")
                );
        }
    }
}
