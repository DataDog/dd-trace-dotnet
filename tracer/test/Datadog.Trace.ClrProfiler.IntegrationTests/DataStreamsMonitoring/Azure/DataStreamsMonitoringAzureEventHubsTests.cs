// <copyright file="DataStreamsMonitoringAzureEventHubsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.DataStreamsMonitoring;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Azure;

[UsesVerify]
public class DataStreamsMonitoringAzureEventHubsTests : TestHelper
{
    private const int ExpectedSpanCount = 5;

    public DataStreamsMonitoringAzureEventHubsTests(ITestOutputHelper output)
        : base("DataStreams.AzureEventHubs", output)
    {
        SetServiceVersion("1.0.0");
    }

    public static IEnumerable<object[]> GetPackageVersions()
        => from packageVersionArray in new string[] { string.Empty }
           select new[] { packageVersionArray };

    [SkippableTheory]
    [MemberData(nameof(GetPackageVersions))]
    [Trait("Category", "EndToEnd")]
    [Trait("SkipInCI", "True")] // Requires a running Azure Event Hubs emulator (or a live Event Hubs namespace via EVENTHUBS_CONNECTION_STRING): the sample's default connection string targets a local emulator.
    public async Task HandleProduceAndConsume(string packageVersion)
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");

        using var assertionScope = new AssertionScope();
        using var agent = EnvironmentHelper.GetMockAgent();
        using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
        {
            agent.SpanFilters.Add(s => s.Name.StartsWith("azure_eventhubs"));
            var spans = await agent.WaitForSpansAsync(ExpectedSpanCount);
            spans.Should().HaveCount(ExpectedSpanCount);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.UseParameters(packageVersion);
            settings.AddDataStreamsScrubber();
            await Verifier.Verify(PayloadsToPoints(agent.DataStreams), settings)
                          .UseFileName($"{nameof(DataStreamsMonitoringAzureEventHubsTests)}.{nameof(HandleProduceAndConsume)}")
                          .DisableRequireUniquePrefix();
        }
    }

    [SkippableTheory]
    [MemberData(nameof(GetPackageVersions))]
    [Trait("Category", "EndToEnd")]
    [Trait("SkipInCI", "True")] // Requires a running Azure Event Hubs emulator (or a live Event Hubs namespace via EVENTHUBS_CONNECTION_STRING): the sample's default connection string targets a local emulator.
    public async Task ValidateSpanTags(string packageVersion)
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");

        using var assertionScope = new AssertionScope();
        using var agent = EnvironmentHelper.GetMockAgent();
        using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
        {
            agent.SpanFilters.Add(s => s.Name.StartsWith("azure_eventhubs"));
            var spans = await agent.WaitForSpansAsync(ExpectedSpanCount);
            spans.Should().HaveCount(ExpectedSpanCount);
            spans.Where(s => s.Tags.ContainsKey("pathway.hash"))
                 .Select(s => s.Tags["messaging.operation"])
                 .Should()
                 .BeEquivalentTo("send", "create", "receive", "receive");
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

        return points
               .OrderBy(s => GetRootHashName(s, points))
               .ThenBy(s => GetHashDepth(s, points))
               .ThenBy(s => s.Hash)
               .ThenBy(s => s.TimestampType)
               .ToList();

        static ulong GetRootHashName(MockDataStreamsStatsPoint point, IReadOnlyCollection<MockDataStreamsStatsPoint> allPoints)
        {
            while (point.ParentHash != 0)
            {
                var parent = allPoints.FirstOrDefault(x => x.Hash == point.ParentHash);
                if (parent is null)
                {
                    // no span with the given Parent Id, so treat this one as the root instead
                    break;
                }

                point = parent;
            }

            return point.Hash;
        }

        static int GetHashDepth(MockDataStreamsStatsPoint point, IReadOnlyCollection<MockDataStreamsStatsPoint> allPoints)
        {
            var depth = 0;
            while (point.ParentHash != 0)
            {
                var parent = allPoints.FirstOrDefault(x => x.Hash == point.ParentHash);
                if (parent is null)
                {
                    // no span with the given Parent Id, so treat this one as the root instead
                    break;
                }

                point = parent;
                depth++;
            }

            return depth;
        }
    }
}
