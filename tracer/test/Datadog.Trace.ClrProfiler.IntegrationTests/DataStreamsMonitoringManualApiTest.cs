// <copyright file="DataStreamsMonitoringManualApiTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.DataStreamsMonitoring;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

[UsesVerify]
public class DataStreamsMonitoringManualApiTest : TestHelper
{
    public DataStreamsMonitoringManualApiTest(ITestOutputHelper output)
        : base("DataStreams.ManualAPI", output)
    {
        SetServiceVersion("1.0.0");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    public async Task ContextPropagation()
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");

        using var agent = EnvironmentHelper.GetMockAgent();
        using var processResult = await RunSampleAndWaitForExit(agent);

        var spans = agent.WaitForSpans(count: 2);
        spans.Should().HaveCount(expected: 2);
        spans[1].TraceId.Should().Be(spans[0].TraceId); // trace context propagation

        var dsPoints = agent.WaitForDataStreamsPoints(statsCount: 2);
        // using span verifier to add all the default scrubbers
        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddDataStreamsScrubber();
        await Verifier.Verify(MockDataStreamsPayload.Normalize(dsPoints), settings)
                      .UseFileName($"{nameof(DataStreamsMonitoringManualApiTest)}.{nameof(ContextPropagation)}")
                      .DisableRequireUniquePrefix();
    }
}
