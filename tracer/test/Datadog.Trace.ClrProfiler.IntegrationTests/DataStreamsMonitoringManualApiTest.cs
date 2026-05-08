// <copyright file="DataStreamsMonitoringManualApiTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Text;
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
        SetEnvironmentVariable(ConfigurationKeys.PropagateProcessTags, "0");

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
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    public async Task TrackTransaction()
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.PropagateProcessTags, "0");

        using var agent = EnvironmentHelper.GetMockAgent();
        using var processResult = await RunSampleAndWaitForExit(agent, arguments: "TrackTransaction");

        // Verify the dsm.transaction.id tag was set on the span
        var spans = await agent.WaitForSpansAsync(count: 2);
        var sendSpan = spans.Should().ContainSingle(s => s.Name == "Samples.DataStreams.ManualAPI.Send").Subject;
        sendSpan.Tags.Should().ContainKey("dsm.transaction.id").WhoseValue.Should().Be("my-transaction-id");

        // Verify the transaction was flushed to the agent
        var payloads = await agent.WaitForDataStreamsTransactionsAsync();
        payloads.Should().NotBeEmpty();
        var bucket = payloads.SelectMany(p => p.Stats).First(b => b.Transactions is { Length: > 0 });

        // Decode transaction bytes: [1 checkpoint_id][8 timestamp_ns][1 id_len][id_bytes]
        var txBytes = bucket.Transactions;
        var checkpointId = txBytes[0];
        var txId = Encoding.UTF8.GetString(txBytes, 10, txBytes[9]);
        txId.Should().Be("my-transaction-id");

        // Decode checkpoint cache: [1 id][1 name_len][name_bytes]
        var cacheBytes = bucket.TransactionCheckpointIds;
        cacheBytes[0].Should().Be(checkpointId);
        var checkpointName = Encoding.UTF8.GetString(cacheBytes, 2, cacheBytes[1]);
        checkpointName.Should().Be("send-checkpoint");
    }
}
