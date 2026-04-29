// <copyright file="DataStreamsMonitoringHttpClientTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

public class DataStreamsMonitoringHttpClientTests : TestHelper
{
    private const string ExtractorConfig =
        """[{"name":"http-tx","type":"HTTP_OUT_HEADERS","value":"X-Transaction-Id"}]""";

    public DataStreamsMonitoringHttpClientTests(ITestOutputHelper output)
        : base("DataStreams.HttpClient", output)
    {
        SetServiceVersion("1.0.0");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    public async Task SubmitsTransactions()
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.TransactionExtractors, ExtractorConfig);

        using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
        using var processResult = await RunSampleAndWaitForExit(agent);

        using var assertionScope = new AssertionScope();

        var dataStreams = await agent.WaitForDataStreamsTransactionsAsync();

        dataStreams.Should().NotBeEmpty("DSM payload should be sent when transactions are tracked");

        var hasTransactions = dataStreams
            .Any(p => p.Stats != null &&
                      p.Stats.Any(b => b.Transactions is { Length: > 0 }));

        hasTransactions.Should().BeTrue("at least one DSM bucket should contain transaction bytes");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    public async Task WhenDisabled_DoesNotSubmitDataStreams()
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "0");
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.TransactionExtractors, ExtractorConfig);

        using var agent = EnvironmentHelper.GetMockAgent();
        using var processResult = await RunSampleAndWaitForExit(agent);

        // We don't expect any streams here, so no point waiting for ages
        var dataStreams = await agent.WaitForDataStreamsAsync(1, timeoutInMilliseconds: 2_000);
        dataStreams.Should().BeEmpty("DSM should not send any data when disabled");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    public async Task WhenInDefaultState_DoesNotTrackTransactions()
    {
        // Do NOT set DD_DATA_STREAMS_MONITORING_ENABLED — this leaves DSM in "default state"
        // where IsTransactionTrackingEnabled = false even when extractors are configured.
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.TransactionExtractors, ExtractorConfig);

        using var agent = EnvironmentHelper.GetMockAgent();
        using var processResult = await RunSampleAndWaitForExit(agent);

        // We don't expect any streams here, so no point waiting for ages
        var dataStreams = await agent.WaitForDataStreamsAsync(1, timeoutInMilliseconds: 2_000);

        var hasTransactions = dataStreams
            .Any(p => p.Stats != null &&
                      p.Stats.Any(b => b.Transactions is { Length: > 0 }));

        hasTransactions.Should().BeFalse(
            "transactions should not be tracked when DSM is in default state (env var not explicitly set)");
    }
}
