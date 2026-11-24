// <copyright file="ProfilerTestRunTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Profiler.IntegrationTests.Helpers.Logs;
using Datadog.Profiler.IntegrationTests.Helpers.Profiles;
using Datadog.Profiler.IntegrationTests.Xunit;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Profiler.IntegrationTests.Helpers.Tests
{
    [Trait("Category", "TestInfrastructure")]
    [ProfilerBeforeAfterTest]
    public class ProfilerTestRunTests
    {
        private readonly ITestOutputHelper _output;

        public ProfilerTestRunTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Dispose_StopsTheAgent()
        {
            var agent = MockDatadogAgent.CreateHttpAgent(_output);
            agent.IsReady.Should().BeTrue();
            var port = agent.Port;

            var run = new ProfilerTestRun(
                agent,
                new ProfileOutput(Array.Empty<PprofProfile>()),
                ProfilerLogSet.Read(Path.Combine(Path.GetTempPath(), "missing-profiler-logs")),
                uploadCount: 0,
                processOutput: string.Empty,
                duration: TimeSpan.Zero,
                outputDirectory: string.Empty,
                logDirectory: string.Empty,
                profileDirectory: string.Empty);

            run.Dispose();
            // Dispose clears the agent field before disposing it, so a second call must not throw. The request below checks the agent is still stopped.
            run.Dispose();

            // On Windows, a refused connection is only reported after about 2s of retries: use a longer timeout.
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var act = () => client.GetAsync($"http://127.0.0.1:{port}/");
            await act.Should().ThrowAsync<HttpRequestException>();
        }

        [Fact]
        public void From_MissingProfileDirectory_FailsWithProcessOutput()
        {
            var runner = new TestApplicationRunner("Samples.Computer01", "net8.0", "Samples.Computer01", _output);
            const string processOutput = "demo-process-output";
            runner.ProcessOutput = processOutput;
            var profileDirectory = runner.Environment.PprofDir;
            Directory.Exists(profileDirectory).Should().BeFalse();

            var agent = MockDatadogAgent.CreateHttpAgent(_output);
            try
            {
                var act = () => ProfilerTestRun.From(runner, agent);
                var exception = act.Should().Throw<XunitException>().Which;
                exception.Message.Should().Contain(profileDirectory);
                exception.Message.Should().Contain(processOutput);
            }
            finally
            {
                agent.Dispose();
            }
        }

        [Fact]
        public void From_EmptyProfileDirectory_ReturnsNoProfiles()
        {
            var runner = new TestApplicationRunner("Samples.Computer01", "net8.0", "Samples.Computer01", _output);
            var profileDirectory = runner.Environment.PprofDir;
            Directory.CreateDirectory(profileDirectory);

            var agent = MockDatadogAgent.CreateHttpAgent(_output);
            using var run = ProfilerTestRun.From(runner, agent);

            run.Profiles.Profiles.Should().BeEmpty();
        }
    }
}
