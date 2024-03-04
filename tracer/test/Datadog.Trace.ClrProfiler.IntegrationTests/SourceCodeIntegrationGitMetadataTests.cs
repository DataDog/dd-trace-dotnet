// <copyright file="SourceCodeIntegrationGitMetadataTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

public class SourceCodeIntegrationGitMetadataTests : TestHelper
{
    public SourceCodeIntegrationGitMetadataTests(ITestOutputHelper output)
        : base("ManualInstrumentation", output)
    {
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task ManualAndAutomatic()
    {
        const int expectedSpans = 36;
        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        using var assert = new AssertionScope();
        using var process = await RunSampleAndWaitForExit(agent);

        var spans = agent.WaitForSpans(expectedSpans);
        spans.Should().HaveCount(expectedSpans);

        // Let's separate the traces
        foreach (var trace in spans.GroupBy(s => s.TraceId))
        {
            // Only a single tagging per trace
            trace.Should().ContainSingle(s => s.Tags.ContainsKey(Trace.Tags.GitCommitSha));
            trace.Should().ContainSingle(s => s.Tags.ContainsKey(Trace.Tags.GitRepositoryUrl));

            // Must be the first span of the trace
            trace.First().Tags.Should().ContainKey(Trace.Tags.GitCommitSha);
            trace.First().Tags.Should().ContainKey(Trace.Tags.GitRepositoryUrl);
        }
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task ManualOnly()
    {
        EnvironmentHelper.SetAutomaticInstrumentation(false);
        // with automatic instrumentation disabled, we don't expect _any_ spans
        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        using var assert = new AssertionScope();
        using var process = await RunSampleAndWaitForExit(agent);

        var spans = agent.WaitForSpans(0, timeoutInMilliseconds: 500);
        spans.Should().BeEmpty();
    }
}
