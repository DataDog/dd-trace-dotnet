// <copyright file="AzureEventGridTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Azure
{
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "2")]
    [Trait("Category", "ArmUnsupported")]
    public class AzureEventGridTests : TracingIntegrationTest
    {
        private const string DefaultTopicEndpoint = "http://localhost:6500/samples-eventgrid-topic/api/events";
        private const string ExpectedOperationName = "azure_eventgrid.send";

        private static readonly string PublisherHost =
            new Uri(Environment.GetEnvironmentVariable("EVENTGRID_TOPIC_ENDPOINT") ?? DefaultTopicEndpoint).Host;

        private static readonly object[][] PublisherTestCases =
        [
            ["SendEventGridEvent", null, true],
            ["SendEventGridEventAsync", null, true],
            ["SendEventGridEvents", 3, false],
            ["SendEventGridEventsAsync", 3, false],
            ["SendCloudEvent", null, true],
            ["SendCloudEventAsync", null, true],
            ["SendCloudEvents", 3, false],
            ["SendCloudEventsAsync", 3, false],
        ];

        private static readonly object[][] PartnerChannelTestCases =
        [
            ["SendCloudEventToChannel", null, true],
            ["SendCloudEventsToChannel", 3, false],
            ["SendCloudEventToChannelAsync", null, true],
            ["SendCloudEventsToChannelAsync", 3, false],
        ];

        public AzureEventGridTests(ITestOutputHelper output)
            : base("AzureEventGrid", output)
        {
            SetEnvironmentVariable("DD_TRACE_AZUREEVENTGRID_ENABLED", "true");
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.AzureEventGrid
               from metadataSchemaVersion in new[] { "v0", "v1" }
               from testCase in PublisherTestCases
               select new[] { packageVersionArray[0], metadataSchemaVersion }.Concat(testCase).ToArray();

        // Partner channel overloads were introduced in Azure.Messaging.EventGrid 4.11.0.
        public static IEnumerable<object[]> GetPartnerChannelEnabledConfig()
            => from packageVersionArray in PackageVersions.AzureEventGrid
               let packageVersion = (string)packageVersionArray[0]
               where string.IsNullOrEmpty(packageVersion) || new Version(packageVersion) >= new Version(4, 11, 0)
               from metadataSchemaVersion in new[] { "v0", "v1" }
               from testCase in PartnerChannelTestCases
               select new object[] { packageVersion, metadataSchemaVersion }.Concat(testCase).ToArray();

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.Tags["span.kind"] switch
            {
                SpanKinds.Producer => span.IsAzureEventGridOutbound(metadataSchemaVersion),
                _ => throw new ArgumentException($"span.Tags[\"span.kind\"] is not a supported value for the Azure EventGrid integration: {span.Tags["span.kind"]}", nameof(span)),
            };

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [MemberData(nameof(GetPartnerChannelEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitEvents(string packageVersion, string metadataSchemaVersion, string testMode, int? expectedBatchMessageCount, bool expectMessageId)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("EVENTGRID_TEST_MODE", testMode);

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(1, timeoutInMilliseconds: 5_000, operationName: ExpectedOperationName, assertExpectedCount: false);

                using var s = new AssertionScope();
                var span = spans.Should().ContainSingle($"Expected one producer span for azure_eventgrid.send ({testMode})").Which;
                var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                result.Success.Should().BeTrue($"Span validation failed: {result}");

                span.Tags["network.destination.name"].Should().Be(PublisherHost);
                span.Resource.Should().Be("eventgrid");

                if (expectedBatchMessageCount is { } batchMessageCount)
                {
                    span.Tags.Should().ContainKey("messaging.batch.message_count").WhoseValue.Should().Be(batchMessageCount.ToString());
                }
                else
                {
                    span.Tags.Should().NotContainKey("messaging.batch.message_count");
                }

                if (expectMessageId)
                {
                    span.Tags.Should().ContainKey("messaging.message_id").WhoseValue.Should().NotBeNullOrEmpty();
                }
                else
                {
                    span.Tags.Should().NotContainKey("messaging.message_id");
                }
            }
        }
    }
}
