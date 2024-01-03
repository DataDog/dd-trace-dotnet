// <copyright file="DataStreamsMonitoringAwsSqsTests.cs" company="Datadog">
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
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AWS;

[Collection(nameof(AwsSqsTests.AwsSqsTestsCollection))]
[Trait("RequiresDockerDependency", "true")]
[UsesVerify]
public class DataStreamsMonitoringAwsSqsTests : TestHelper
{
    public DataStreamsMonitoringAwsSqsTests(ITestOutputHelper output)
        : base("AWS.SQS", output)
    {
    }

    public static IEnumerable<object[]> GetEnabledConfig()
    {
        return from packageVersionArray in PackageVersions.AwsSqs
               select new[] { packageVersionArray[0] };
    }

    [SkippableTheory]
    [MemberData(nameof(GetEnabledConfig))]
    [Trait("Category", "EndToEnd")]
    public async Task SubmitsDsmMetrics(string packageVersion)
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");

        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        using (RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
        {
#if NETFRAMEWORK
            var expectedCount = 56;
            var frameworkName = "NetFramework";
#else
            var expectedCount = 28;
            var frameworkName = "NetCore";
#endif
            var spans = agent.WaitForSpans(expectedCount);
            var sqsSpans = spans.Where(
                span => span.Tags.TryGetValue("component", out var component) && component == "aws-sdk");

            sqsSpans.Should().NotBeEmpty();

            var taggedSpans = spans.Where(s => s.Tags.ContainsKey("pathway.hash"));
            taggedSpans.Should().HaveCount(expected: 16);

            var dsPoints = agent.WaitForDataStreamsPoints(statsCount: 16);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.UseParameters(packageVersion);
            settings.AddDataStreamsScrubber();
            await Verifier.Verify(PayloadsToDeduplicatedPoints(dsPoints), settings)
                          .UseFileName($"{nameof(DataStreamsMonitoringAwsSqsTests)}.{nameof(SubmitsDsmMetrics)}.{frameworkName}")
                          .DisableRequireUniquePrefix();

            telemetry.AssertIntegrationEnabled(IntegrationId.AwsSqs);
        }
    }

    private static List<MockDataStreamsStatsPoint> PayloadsToDeduplicatedPoints(IImmutableList<MockDataStreamsPayload> dsPoints)
    {
        var points = new List<MockDataStreamsStatsPoint>();
        foreach (var payload in dsPoints)
        {
            foreach (var bucket in payload.Stats)
            {
                foreach (var point in bucket.Stats)
                {
                    // deduplicate points that may be aggregated on the agent side, to get rid of that source of random
                    if (!points.Any(x => x.Hash == point.Hash && x.ParentHash == point.ParentHash && x.TimestampType == point.TimestampType))
                    {
                        points.Add(point);
                    }
                }
            }
        }

        // order in a predictable way to make sure snapshots will look the same
        return points.OrderBy(s => s.Hash).ThenBy(s => s.ParentHash).ThenBy(s => s.TimestampType).ToList();
    }
}
