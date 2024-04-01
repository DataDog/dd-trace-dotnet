// <copyright file="DataStreamsMonitoringRabbitMQTests.cs" company="Datadog">
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
    }

    [SkippableTheory]
    [MemberData(nameof(PackageVersions.RabbitMQ), MemberType = typeof(PackageVersions))]
    [Trait("Category", "EndToEnd")]
    public async Task HandleProduceAndConsume(string packageVersion)
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");

        using var assertionScope = new AssertionScope();
        using var agent = EnvironmentHelper.GetMockAgent();
        using (await RunSampleAndWaitForExit(agent, arguments: $"{TestPrefix}", packageVersion: packageVersion))
        {
            var spans = agent.WaitForSpans(31);
            spans.Should().HaveCount(31);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.UseParameters(packageVersion);
            settings.AddDataStreamsScrubber();
            await Verifier.Verify(PayloadsToPoints(agent.DataStreams), settings)
                          .UseFileName($"{nameof(DataStreamsMonitoringRabbitMQTests)}.{nameof(HandleProduceAndConsume)}")
                          .DisableRequireUniquePrefix();
        }
    }

    [SkippableTheory]
    [MemberData(nameof(PackageVersions.RabbitMQ), MemberType = typeof(PackageVersions))]
    [Trait("Category", "EndToEnd")]
    public async Task ValidateSpanTags(string packageVersion)
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");

        using var assertionScope = new AssertionScope();
        using var agent = EnvironmentHelper.GetMockAgent();
        using (await RunSampleAndWaitForExit(agent, arguments: $"{TestPrefix}", packageVersion: packageVersion))
        {
            var spans = agent.WaitForSpans(31);
            spans.Should().HaveCount(31);
            var taggedSpans = spans.Where(s => s.Tags.ContainsKey("pathway.hash"));
            taggedSpans.Should().HaveCount(13);
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

        return points.OrderBy(s => s.Hash).ThenBy(s => s.TimestampType).ToList();
    }
}
