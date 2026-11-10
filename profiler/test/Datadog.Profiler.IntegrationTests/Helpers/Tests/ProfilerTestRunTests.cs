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
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Helpers.Tests
{
    [Trait("Category", "TestInfrastructure")]
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
            run.Dispose();

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var act = () => client.GetAsync($"http://127.0.0.1:{port}/");
            await act.Should().ThrowAsync<HttpRequestException>();
        }
    }
}
