// <copyright file="HttpRequestProfilerTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Network
{
    public class HttpRequestProfilerTest
    {
        private const string All = "--iterations 5 --scenario 7";
        private const string Redirect = "--iterations 5 --scenario 1";
        private const string Error = "--iterations 5 --scenario 2";
        private const string Blog = "--iterations 1 --scenario 4";

        private readonly ITestOutputHelper _output;

        public HttpRequestProfilerTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.ParallelCountSites", new[] { "net5.0", "net6.0" })]
        public void ShouldNotGetHttpSamplesInOldRuntimeVersions(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: All);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.HttpProfilingEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.ForceHttpSamplingEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            // HTTP events are not emitted in .NET 5 nor .NET 6
            var samples = SamplesHelper.GetSamples(runner.Environment.PprofDir);
            samples.Should().BeEmpty();
        }

        [TestAppFact("Samples.ParallelCountSites", new[] { "net7.0", "net8.0", "net9.0" })]
        public void ShouldNotGetHttpSamplesWhenDefaultSampling(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: All);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.HttpProfilingEnabled, "1");
            // EnvironmentVariables.ForceHttpSamplingEnabled is not set --> need span + min duration to be sampled

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            var samples = SamplesHelper.GetSamples(runner.Environment.PprofDir);
            samples.Should().BeEmpty();
        }

        [TestAppFact("Samples.ParallelCountSites", new[] { "net7.0", "net8.0", "net9.0" })]
        public void ShouldGetHttpSamples(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: All);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.HttpProfilingEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.ForceHttpSamplingEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            // only network profiler enabled so should only see one value per sample
            SamplesHelper.CheckSamplesValueCount(runner.Environment.PprofDir, 1);
            Assert.True(SamplesHelper.IsLabelPresent(runner.Environment.PprofDir, "request url"));
            Assert.True(SamplesHelper.IsLabelPresent(runner.Environment.PprofDir, "request status code"));
        }

        [TestAppFact("Samples.ParallelCountSites", new[] { "net7.0", "net8.0", "net9.0" })]
        public void ShouldGetRedirect(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Redirect);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.HttpProfilingEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.ForceHttpSamplingEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            // only network profiler enabled so should only see one value per sample
            var samples = SamplesHelper.GetSamples(runner.Environment.PprofDir, "request-time");
            samples.Should().NotBeEmpty();

            foreach (var (stackTrace, labels, values) in samples)
            {
                Assert.True(stackTrace.FramesCount > 0);
                Assert.True(values.Length == 1);

                var urlLabel = labels.FirstOrDefault(l => l.Name == "request url");
                urlLabel.Name.Should().NotBeNullOrWhiteSpace();
                urlLabel.Value.Should().NotBeNullOrWhiteSpace();

                // some unexpected requests could be emitted (e.g. telemetry, etc.)
                if (!urlLabel.Value.Contains("Maoni"))
                {
                    continue;
                }

                var redirectLabel = labels.FirstOrDefault(l => l.Name == "redirect url");
                redirectLabel.Name.Should().NotBeNullOrWhiteSpace();

                // in .NET 7, the redirect is detected but no redirected url is available
                if (framework != "net7.0")
                {
                    redirectLabel.Value.Should().NotBeNullOrWhiteSpace();
                }

                // request/response happens for all runtimes
                var requestDurationLabel = labels.FirstOrDefault(l => l.Name == "request.duration");
                requestDurationLabel.Name.Should().NotBeNullOrWhiteSpace();
                requestDurationLabel.Value.Should().NotBeNullOrWhiteSpace();
                var responseDurationLabel = labels.FirstOrDefault(l => l.Name == "response_content.duration");
                responseDurationLabel.Name.Should().NotBeNullOrWhiteSpace();
                responseDurationLabel.Value.Should().NotBeNullOrWhiteSpace();
            }
        }

        [TestAppFact("Samples.ParallelCountSites", new[] { "net7.0", "net8.0", "net9.0" })]
        public void ShouldGetError(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Error);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.HttpProfilingEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.ForceHttpSamplingEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            // only network profiler enabled so should only see one value per sample
            var samples = SamplesHelper.GetSamples(runner.Environment.PprofDir, "request-time");
            samples.Should().NotBeEmpty();
            foreach (var (stackTrace, labels, values) in samples)
            {
                Assert.True(stackTrace.FramesCount > 0);
                Assert.True(values.Length == 1);

                var urlLabel = labels.FirstOrDefault(l => l.Name == "request url");
                urlLabel.Name.Should().NotBeNullOrWhiteSpace();
                urlLabel.Value.Should().NotBeNullOrWhiteSpace();

                // some unexpected requests could be emitted (e.g. telemetry, etc.)
                if (urlLabel.Value != "http://this.does.not.exist.com:80/sorry")
                {
                    continue;
                }

                var errorLabel = labels.FirstOrDefault(l => l.Name == "request error");
                errorLabel.Name.Should().NotBeNullOrWhiteSpace();
                errorLabel.Value.Should().NotBeNullOrWhiteSpace();

                var requestDurationLabel = labels.FirstOrDefault(l => l.Name == "request.duration");
                requestDurationLabel.Name.Should().BeNullOrWhiteSpace();
                requestDurationLabel.Value.Should().BeNullOrWhiteSpace();
                var responseDurationLabel = labels.FirstOrDefault(l => l.Name == "response_content.duration");
                responseDurationLabel.Name.Should().BeNullOrWhiteSpace();
                responseDurationLabel.Value.Should().BeNullOrWhiteSpace();
            }
        }

        [TestAppFact("Samples.ParallelCountSites", new[] { "net7.0", "net8.0", "net9.0" })]
        public void ShouldGetDetails(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Blog);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.HttpProfilingEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.ForceHttpSamplingEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            // only network profiler enabled so should only see one value per sample
            var samples = SamplesHelper.GetSamples(runner.Environment.PprofDir, "request-time");
            samples.Should().NotBeEmpty();
            foreach (var (stackTrace, labels, values) in samples)
            {
                Assert.True(stackTrace.FramesCount > 0);
                Assert.True(values.Length == 1);

                var urlLabel = labels.FirstOrDefault(l => l.Name == "request url");
                urlLabel.Name.Should().NotBeNullOrWhiteSpace();
                urlLabel.Value.Should().NotBeNullOrWhiteSpace();

                // some unexpected requests could be emitted (e.g. telemetry, etc.)
                if (urlLabel.Value != "https://www.datadoghq.com:443/blog/engineering/dotnet-continuous-profiler-part-4/")
                {
                    continue;
                }

                var startTimeLabel = labels.FirstOrDefault(l => l.Name == "start_timestamp_ns");
                startTimeLabel.Name.Should().NotBeNullOrWhiteSpace();
                startTimeLabel.Value.Should().NotBeNullOrWhiteSpace();

                var errorLabel = labels.FirstOrDefault(l => l.Name == "response.thread_id");
                errorLabel.Name.Should().NotBeNullOrWhiteSpace();
                errorLabel.Value.Should().NotBeNullOrWhiteSpace();

                var dnsSuccessLabel = labels.FirstOrDefault(l => l.Name == "dns.success");
                dnsSuccessLabel.Name.Should().NotBeNullOrWhiteSpace();
                Assert.True(dnsSuccessLabel.Value == "true");

                var dnsDurationLabel = labels.FirstOrDefault(l => l.Name == "dns.duration");
                dnsDurationLabel.Name.Should().NotBeNullOrWhiteSpace();
                dnsDurationLabel.Value.Should().NotBeNullOrWhiteSpace();

                var socketDurationLabel = labels.FirstOrDefault(l => l.Name == "socket.duration");
                socketDurationLabel.Name.Should().NotBeNullOrWhiteSpace();
                socketDurationLabel.Value.Should().NotBeNullOrWhiteSpace();

                var securityDurationLabel = labels.FirstOrDefault(l => l.Name == "sec.duration");
                securityDurationLabel.Name.Should().NotBeNullOrWhiteSpace();
                securityDurationLabel.Value.Should().NotBeNullOrWhiteSpace();

                var requestDurationLabel = labels.FirstOrDefault(l => l.Name == "request.duration");
                requestDurationLabel.Name.Should().NotBeNullOrWhiteSpace();
                requestDurationLabel.Value.Should().NotBeNullOrWhiteSpace();

                var responseDurationLabel = labels.FirstOrDefault(l => l.Name == "response_content.duration");
                responseDurationLabel.Name.Should().NotBeNullOrWhiteSpace();
                responseDurationLabel.Value.Should().NotBeNullOrWhiteSpace();

                var responseThreadIdLabel = labels.FirstOrDefault(l => l.Name == "response.thread_id");
                responseThreadIdLabel.Name.Should().NotBeNullOrWhiteSpace();
                responseThreadIdLabel.Value.Should().NotBeNullOrWhiteSpace();

                var responseThreadNameLabel = labels.FirstOrDefault(l => l.Name == "response.thread_name");
                responseThreadNameLabel.Name.Should().NotBeNullOrWhiteSpace();
                responseThreadNameLabel.Value.Should().NotBeNullOrWhiteSpace();

                var requestStatusCodeLabel = labels.FirstOrDefault(l => l.Name == "request status code");
                requestStatusCodeLabel.Name.Should().NotBeNullOrWhiteSpace();
                Assert.True(requestStatusCodeLabel.Value == "200");
            }
        }
    }
}
