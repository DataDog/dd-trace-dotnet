// <copyright file="DataStreamsMonitoringRabbitMQTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.DataStreamsMonitoring;
using Datadog.Trace.Vendors.StatsdClient.Statistic;
using FluentAssertions;
using FluentAssertions.Execution;
using ICSharpCode.Decompiler.Util;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

[UsesVerify]
[Trait("RequiresDockerDependency", "true")]
public class DataStreamsMonitoringRabbitMQTests : TestHelper
{
    public DataStreamsMonitoringRabbitMQTests(ITestOutputHelper output)
        : base("DataStreams.RabbitMQ", output)
    {
        SetServiceVersion("1.0.0");
        EnableDebugMode();
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    public async Task HandleProduceAndConsume()
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");

        using var assertionScope = new AssertionScope();
        using var agent = EnvironmentHelper.GetMockAgent();
        using (RunSampleAndWaitForExit(agent, arguments: $"{TestPrefix}"))
        {
            var payloads = agent.WaitForDataStreams(4);
            var spans = agent.WaitForSpans(4);

            payloads.Should().NotBeEmpty();
            spans.Where(s => s.Tags.ContainsKey("pathway.hash")).Should().HaveCount(4);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.ModifySerialization(
                _ =>
                {
                    _.MemberConverter<MockDataStreamsStatsPoint, byte[]>(
                        x => x.EdgeLatency,
                        (_, v) =>  v?.Length == 0 ? v : new byte[] { 0xFF });
                    _.MemberConverter<MockDataStreamsStatsPoint, byte[]>(
                        x => x.PathwayLatency,
                        (_, v) =>  v?.Length == 0 ? v : new byte[] { 0xFF });
                });
            await Verifier.Verify(PayloadsToPoints(payloads), settings)
                          .UseFileName($"{nameof(DataStreamsMonitoringRabbitMQTests)}.{nameof(HandleProduceAndConsume)}")
                          .DisableRequireUniquePrefix();
        }
    }

    private static IList<MockDataStreamsStatsPoint> PayloadsToPoints(IImmutableList<MockDataStreamsPayload> payloads)
    {
        var points = new List<MockDataStreamsStatsPoint>();
        foreach (var payload in payloads)
        {
            foreach (var bucket in payload.Stats)
            {
                if (bucket.Stats != null)
                {
                    points.AddRange(bucket.Stats);
                }
            }
        }

        points.SortBy(s => s.Hash);
        return points.ToList();
    }
}
