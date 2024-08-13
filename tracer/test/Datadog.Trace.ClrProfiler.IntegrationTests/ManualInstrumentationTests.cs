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

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task ManualAndAutomatic()
    {
        SetEnvironmentVariable("AUTO_INSTRUMENT_ENABLED", "1");
        const int expectedSpans = 36;
        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        using var assert = new AssertionScope();
        using var process = await RunSampleAndWaitForExit(agent);

        var spans = agent.WaitForSpans(expectedSpans);
        spans.Should().HaveCount(expectedSpans);

        var settings = VerifyHelper.GetSpanVerifierSettings();

        await VerifyHelper.VerifySpans(spans, settings);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task ManualOnly()
    {
        SetEnvironmentVariable("AUTO_INSTRUMENT_ENABLED", "0");
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
