// <copyright file="DataStreamsMonitoringAzureServiceBusTests.cs" company="Datadog">
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
public class DataStreamsMonitoringAzureServiceBusTests : TestHelper
{
    public DataStreamsMonitoringAzureServiceBusTests(ITestOutputHelper output)
        : base("DataStreams.AzureServiceBus", output)
    {
        SetServiceVersion("1.0.0");
    }

    public static IEnumerable<object[]> GetPackageVersions()
        => from packageVersionArray in new string[] { string.Empty }
           select new string[] { packageVersionArray };

    [SkippableTheory]
    [MemberData(nameof(GetPackageVersions))]
    [Trait("Category", "EndToEnd")]
    [Trait("SkipInCI", "True")] // "This has only been tested on a live Azure Service Bus namespace using a connection string. Unskip this if you'd like to run locally or if you've correctly configured piotr-rojek/devopsifyme-sbemulator in CI"
    public async Task HandleProduceAndConsume(string packageVersion)
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");
        SetEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "true");

        // If you want to use a custom connection string, set it here
        // SetEnvironmentVariable("ASB_CONNECTION_STRING", null);

        using var assertionScope = new AssertionScope();
        using var agent = EnvironmentHelper.GetMockAgent();
        using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
        {
            agent.SpanFilters.Add(s => s.Tags.TryGetValue("messaging.system", out var value) && value == "servicebus"); // Exclude the Admin requests
            var spans = agent.WaitForSpans(23);
            spans.Should().HaveCount(23);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.UseParameters(packageVersion);
            settings.AddDataStreamsScrubber();
            await Verifier.Verify(PayloadsToPoints(agent.DataStreams), settings)
                          .UseFileName($"{nameof(DataStreamsMonitoringAzureServiceBusTests)}.{nameof(HandleProduceAndConsume)}")
                          .DisableRequireUniquePrefix();
        }
    }

    [SkippableTheory]
    [MemberData(nameof(GetPackageVersions))]
    [Trait("Category", "EndToEnd")]
    [Trait("SkipInCI", "True")] // This has only been tested on a live Azure Service Bus namespace using a connection string. Unskip this if you'd like to run locally or if you've correctly configured piotr-rojek/devopsifyme-sbemulator in CI
    public async Task ValidateSpanTags(string packageVersion)
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");
        SetEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "true");

        // If you want to use a custom connection string, set it here
        // SetEnvironmentVariable("ASB_CONNECTION_STRING", null);

        using var assertionScope = new AssertionScope();
        using var agent = EnvironmentHelper.GetMockAgent();
        using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
        {
            agent.SpanFilters.Add(s => s.Tags.TryGetValue("messaging.system", out var value) && value == "servicebus"); // Exclude the Admin requests
            var spans = agent.WaitForSpans(23);
            spans.Should().HaveCount(23);
            var taggedSpans = spans.Where(s => s.Tags.ContainsKey("pathway.hash"));
            taggedSpans.Should().HaveCount(9); // 4 messages published, 5 messages consumed
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
