// <copyright file="ManualInstrumentationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

[UsesVerify]
public class ManualInstrumentationTests : TestHelper
{
    public ManualInstrumentationTests(ITestOutputHelper output)
        : base("ManualInstrumentation", output)
    {
    }

    [SkippableFact(Skip = "Their flaky, need to address the root cause around Tracer lifetimes")]
    [Trait("RunOnWindows", "True")]
    public async Task ManualAndAutomatic()
    {
        const int expectedSpans = 36;
        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        using var assert = new AssertionScope();
        using var process = RunSampleAndWaitForExit(agent);

        var spans = agent.WaitForSpans(expectedSpans);
        spans.Should().HaveCount(expectedSpans);

        var settings = VerifyHelper.GetSpanVerifierSettings();

        await VerifyHelper.VerifySpans(spans, settings);
    }

    [SkippableFact(Skip = "Their flaky, need to address the root cause around Tracer lifetimes")]
    [Trait("RunOnWindows", "True")]
    public async Task ManualOnly()
    {
        EnvironmentHelper.SetAutomaticInstrumentation(false);
        const int expectedSpans = 29;
        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        using var assert = new AssertionScope();
        using var process = RunSampleAndWaitForExit(agent);

        var spans = agent.WaitForSpans(expectedSpans);
        spans.Should().HaveCount(expectedSpans);

        var settings = VerifyHelper.GetSpanVerifierSettings();

        await VerifyHelper.VerifySpans(spans, settings);
    }
}
