// <copyright file="DataStreamsMonitoringManualApiTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
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

        var spans = await agent.WaitForSpansAsync(count: 2);
        spans.Should().HaveCount(expected: 2);
        spans[1].TraceId.Should().Be(spans[0].TraceId); // trace context propagation

        var dsPoints = await agent.WaitForDataStreamsPointsAsync(statsCount: 2);
        // using span verifier to add all the default scrubbers
        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddDataStreamsScrubber();
        await Verifier.Verify(MockDataStreamsPayload.Normalize(dsPoints), settings)
                      .UseFileName($"{nameof(DataStreamsMonitoringManualApiTest)}.{nameof(ContextPropagation)}")
                      .DisableRequireUniquePrefix();

        // Verify transaction tracking: both spans tagged, produce/consume tracked with the same transaction ID
        var transactions = dsPoints
                          .SelectMany(p => p.Stats)
                          .SelectMany(b => b.DecodeTransactions())
                          .ToList();

        transactions.Should().HaveCount(2, "one produce and one consume transaction should be recorded");
        transactions.Should().ContainSingle(t => t.CheckpointName == "queue-produce");
        transactions.Should().ContainSingle(t => t.CheckpointName == "queue-consume");

        var transactionId = transactions[0].TransactionId;
        transactions.Should().OnlyContain(t => t.TransactionId == transactionId, "producer and consumer should share the same transaction ID");

        spans.Should().OnlyContain(s => s.Tags.ContainsKey("dsm.transaction.id"), "each span should be tagged with dsm.transaction.id");
        spans.Should().OnlyContain(s => s.Tags["dsm.transaction.id"] == transactionId, "span tags should match the tracked transaction ID");
    }
}
