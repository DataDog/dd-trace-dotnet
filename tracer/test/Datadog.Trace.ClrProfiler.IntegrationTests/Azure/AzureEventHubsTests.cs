// <copyright file="AzureEventHubsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
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
    public class AzureEventHubsTests : TracingIntegrationTest
    {
        public AzureEventHubsTests(ITestOutputHelper output)
            : base("AzureEventHubs", output)
        {
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.AzureEventHubs
               from metadataSchemaVersion in new[] { "v0", "v1" }
               select new[] { packageVersionArray[0], metadataSchemaVersion };

        public static IEnumerable<object[]> GetEnabledConfigWithMessageCount()
            => from packageVersionArray in PackageVersions.AzureEventHubs
               from metadataSchemaVersion in new[] { "v0", "v1" }
               from messageCount in new[] { 1, 3 }
               select new object[] { packageVersionArray[0], metadataSchemaVersion, messageCount };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.Tags["span.kind"] switch
        {
            SpanKinds.Consumer => span.IsAzureEventHubsInbound(metadataSchemaVersion),
            SpanKinds.Producer when span.Name == "azure_eventhubs.create" => span.IsAzureEventHubsCreate(metadataSchemaVersion),
            SpanKinds.Producer => span.IsAzureEventHubsOutbound(metadataSchemaVersion),
            _ => throw new ArgumentException($"span.Tags[\"span.kind\"] is not a supported value for the Azure EventHubs integration: {span.Tags["span.kind"]}", nameof(span)),
        };

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task TestEventHubsMessageBatchIntegration(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("DD_TRACE_AZUREEVENTHUBS_ENABLED", "true");
            SetEnvironmentVariable("DD_TRACE_AZURE_EVENTHUBS_BATCH_LINKS_ENABLED", "true");
            SetEnvironmentVariable("EVENTHUBS_TEST_MODE", "TestEventHubsMessageBatch");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(5, timeoutInMilliseconds: 30000, assertExpectedCount: false);

                using var s = new AssertionScope();

                var createSpans = spans.Where(span => span.Name == "azure_eventhubs.create").ToList();
                var sendSpans = spans.Where(span => span.Name == "azure_eventhubs.send").ToList();
                var receiveSpans = spans.Where(span => span.Name == "azure_eventhubs.receive").ToList();

                createSpans.Should().HaveCount(3, "Expected 3 TryAdd spans with azure_eventhubs.create operation");
                sendSpans.Should().HaveCount(1, "Expected 1 SendAsync span with azure_eventhubs.send operation");
                receiveSpans.Should().HaveCountGreaterOrEqualTo(1, "Expected at least 1 receive span");

                var individualMessageSpans = createSpans.Where(s => s.Resource == "samples-eventhubs-hub").ToList();
                individualMessageSpans.Should().HaveCount(3, "Expected 3 individual message spans from TryAdd operations");

                var batchSendSpans = sendSpans.Where(s => s.Resource == "samples-eventhubs-hub").ToList();
                batchSendSpans.Should().HaveCount(1, "Expected 1 batch send span from SendAsync operation");

                foreach (var span in spans)
                {
                    var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"Span validation failed: {result}");
                }

                var allProducerSpans = createSpans.Concat(sendSpans).ToList();
                ValidateSpanLinks(allProducerSpans, receiveSpans, spans);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfigWithMessageCount))]
        [Trait("Category", "EndToEnd")]
        public async Task TestEventHubsEnumerableIntegration(string packageVersion, string metadataSchemaVersion, int messageCount)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("DD_TRACE_AZUREEVENTHUBS_ENABLED", "true");
            SetEnvironmentVariable("DD_TRACE_AZURE_EVENTHUBS_BATCH_LINKS_ENABLED", "true");
            SetEnvironmentVariable("EVENTHUBS_TEST_MODE", "TestEventHubsEnumerable");
            SetEnvironmentVariable("EVENTHUBS_MESSAGE_COUNT", messageCount.ToString());

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(2, timeoutInMilliseconds: 30000, assertExpectedCount: false);

                using var s = new AssertionScope();

                var sendSpans = spans.Where(span => span.Name == "azure_eventhubs.send").ToList();
                var receiveSpans = spans.Where(span => span.Name == "azure_eventhubs.receive").ToList();

                sendSpans.Should().HaveCount(1, "Expected 1 SendAsync span with azure_eventhubs.send operation for enumerable");
                receiveSpans.Should().HaveCountGreaterOrEqualTo(1, "Expected at least 1 receive span");

                var enumerableSendSpans = sendSpans.Where(s => s.Resource == "samples-eventhubs-hub").ToList();
                enumerableSendSpans.Should().HaveCount(1, "Expected 1 enumerable send span from SendAsync operation");

                foreach (var span in spans)
                {
                    var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"Span validation failed: {result}");
                }

                ValidateSpanLinks(sendSpans, receiveSpans, spans);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task TestEventHubsMessageBatchIntegrationWithoutBatchLinks(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("DD_TRACE_AZUREEVENTHUBS_ENABLED", "true");
            SetEnvironmentVariable("DD_TRACE_AZURE_EVENTHUBS_BATCH_LINKS_ENABLED", "false");
            SetEnvironmentVariable("EVENTHUBS_TEST_MODE", "TestEventHubsMessageBatch");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(2, timeoutInMilliseconds: 30000, assertExpectedCount: false);

                using var s = new AssertionScope();

                var sendSpans = spans.Where(span => span.Name == "azure_eventhubs.send").ToList();
                var receiveSpans = spans.Where(span => span.Name == "azure_eventhubs.receive").ToList();

                sendSpans.Should().HaveCount(1, "Expected 1 SendAsync span (no individual TryAdd spans when batch links disabled)");
                receiveSpans.Should().HaveCountGreaterOrEqualTo(1, "Expected at least 1 receive span");

                foreach (var span in spans)
                {
                    var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"EventHubs span validation failed: {result}");
                }

                foreach (var receiveSpan in receiveSpans)
                {
                    receiveSpan.SpanLinks.Should().BeNullOrEmpty("No span links expected when batch linking is disabled");
                }
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfigWithMessageCount))]
        [Trait("Category", "EndToEnd")]
        public async Task TestEventHubsEnumerableIntegrationWithoutBatchLinks(string packageVersion, string metadataSchemaVersion, int messageCount)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("DD_TRACE_AZUREEVENTHUBS_ENABLED", "true");
            SetEnvironmentVariable("DD_TRACE_AZURE_EVENTHUBS_BATCH_LINKS_ENABLED", "false");
            SetEnvironmentVariable("EVENTHUBS_TEST_MODE", "TestEventHubsEnumerable");
            SetEnvironmentVariable("EVENTHUBS_MESSAGE_COUNT", messageCount.ToString());

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(2, timeoutInMilliseconds: 30000, assertExpectedCount: false);

                using var s = new AssertionScope();

                var sendSpans = spans.Where(span => span.Name == "azure_eventhubs.send").ToList();
                var receiveSpans = spans.Where(span => span.Name == "azure_eventhubs.receive").ToList();

                sendSpans.Should().HaveCount(1, "Expected 1 SendAsync span for enumerable (no individual event spans when batch links disabled)");
                receiveSpans.Should().HaveCountGreaterOrEqualTo(1, "Expected at least 1 receive span");

                var createSpans = spans.Where(span => span.Name == "azure_eventhubs.create").ToList();
                createSpans.Should().BeEmpty("Expected no individual message spans (azure_eventhubs.create) when batch links disabled");

                foreach (var span in spans)
                {
                    var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"EventHubs span validation failed: {result}");
                }

                foreach (var receiveSpan in receiveSpans)
                {
                    receiveSpan.SpanLinks.Should().BeNullOrEmpty("No span links expected when batch linking is disabled");
                }
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task TestEventHubsBufferedProducerIntegration(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("DD_TRACE_AZUREEVENTHUBS_ENABLED", "true");
            SetEnvironmentVariable("DD_TRACE_AZURE_EVENTHUBS_BATCH_LINKS_ENABLED", "true");
            SetEnvironmentVariable("EVENTHUBS_TEST_MODE", "TestEventHubsBufferedProducer");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(5, timeoutInMilliseconds: 30000, assertExpectedCount: false);

                using var s = new AssertionScope();

                var createSpans = spans.Where(span => span.Name == "azure_eventhubs.create").ToList();
                var sendSpans = spans.Where(span => span.Name == "azure_eventhubs.send").ToList();
                var receiveSpans = spans.Where(span => span.Name == "azure_eventhubs.receive").ToList();

                createSpans.Should().HaveCount(3, "Expected 3 TryAdd spans with azure_eventhubs.create operation");
                sendSpans.Should().HaveCount(1, "Expected 1 buffered send span with azure_eventhubs.send operation");
                receiveSpans.Should().HaveCountGreaterOrEqualTo(1, "Expected at least 1 receive span");

                var individualMessageSpans = createSpans.Where(s => s.Resource == "samples-eventhubs-hub").ToList();
                individualMessageSpans.Should().HaveCount(3, "Expected 3 individual message spans from TryAdd operations");

                var bufferedSendSpans = sendSpans.Where(s => s.Resource == "samples-eventhubs-hub").ToList();
                bufferedSendSpans.Should().HaveCount(1, "Expected 1 buffered send span from FlushAsync operation");

                foreach (var span in spans)
                {
                    var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"Span validation failed: {result}");
                }

                var allProducerSpans = createSpans.Concat(sendSpans).ToList();
                ValidateSpanLinks(allProducerSpans, receiveSpans, spans);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task TestEventHubsBufferedProducerIntegrationWithoutBatchLinks(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("DD_TRACE_AZUREEVENTHUBS_ENABLED", "true");
            SetEnvironmentVariable("DD_TRACE_AZURE_EVENTHUBS_BATCH_LINKS_ENABLED", "false");
            SetEnvironmentVariable("EVENTHUBS_TEST_MODE", "TestEventHubsBufferedProducer");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(2, timeoutInMilliseconds: 30000, assertExpectedCount: false);

                using var s = new AssertionScope();

                var sendSpans = spans.Where(span => span.Name == "azure_eventhubs.send").ToList();
                var receiveSpans = spans.Where(span => span.Name == "azure_eventhubs.receive").ToList();

                sendSpans.Should().HaveCount(1, "Expected 1 buffered send span (no individual TryAdd spans when batch links disabled)");
                receiveSpans.Should().HaveCountGreaterOrEqualTo(1, "Expected at least 1 receive span");

                var createSpans = spans.Where(span => span.Name == "azure_eventhubs.create").ToList();
                createSpans.Should().BeEmpty("Expected no individual message spans (azure_eventhubs.create) when batch links disabled");

                foreach (var span in spans)
                {
                    var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"EventHubs span validation failed: {result}");
                }

                foreach (var receiveSpan in receiveSpans)
                {
                    receiveSpan.SpanLinks.Should().BeNullOrEmpty("No span links expected when batch linking is disabled");
                }
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task TestEventHubsIntegrationDisabled(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("DD_TRACE_AZUREEVENTHUBS_ENABLED", "false");

            var testModes = new[] { "TestEventHubsMessageBatch", "TestEventHubsEnumerable", "TestEventHubsBufferedProducer" };

            foreach (var testMode in testModes)
            {
                SetEnvironmentVariable("EVENTHUBS_TEST_MODE", testMode);

                using (var agent = EnvironmentHelper.GetMockAgent())
                using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
                {
                    var spans = await agent.WaitForSpansAsync(1, timeoutInMilliseconds: 1000, assertExpectedCount: false);
                    spans.Should().BeEmpty($"Expected no spans when DD_TRACE_AZUREEVENTHUBS_ENABLED is false for test mode {testMode}");
                }
            }
        }

        private static void ValidateSpanLinks(
            IList<MockSpan> sendSpans,
            IList<MockSpan> receiveSpans,
            IReadOnlyCollection<MockSpan> allSpans)
        {
            sendSpans.Should().NotBeEmpty("Need producer spans to validate span links");
            receiveSpans.Should().NotBeEmpty("Need consumer spans to validate span links");

            var spanLinksFoundOnBatchSend = false;
            var spanLinksFoundOnReceive = false;

            var batchSendSpans = sendSpans.Where(s => s.Name == "azure_eventhubs.send").ToList();
            var individualMessageSpans = sendSpans.Where(s => s.Name == "azure_eventhubs.create").ToList();

            foreach (var batchSendSpan in batchSendSpans)
            {
                if (batchSendSpan.SpanLinks != null && batchSendSpan.SpanLinks.Count > 0)
                {
                    spanLinksFoundOnBatchSend = true;

                    foreach (var link in batchSendSpan.SpanLinks)
                    {
                        var linkedIndividualSpan = individualMessageSpans.FirstOrDefault(s => s.TraceId == link.TraceIdLow && s.SpanId == link.SpanId);
                        linkedIndividualSpan.Should().NotBeNull(
                            $"Batch send span {batchSendSpan.SpanId} has link to span {link.SpanId} in trace {link.TraceIdLow}, " +
                            $"but corresponding individual message span not found");
                    }
                }
            }

            foreach (var receiveSpan in receiveSpans)
            {
                if (receiveSpan.SpanLinks != null && receiveSpan.SpanLinks.Count > 0)
                {
                    spanLinksFoundOnReceive = true;

                    foreach (var link in receiveSpan.SpanLinks)
                    {
                        var linkedSendSpan = sendSpans.FirstOrDefault(s => s.TraceId == link.TraceIdLow && s.SpanId == link.SpanId);
                        linkedSendSpan.Should().NotBeNull(
                            $"Receive span {receiveSpan.SpanId} has link to span {link.SpanId} in trace {link.TraceIdLow}, " +
                            $"but corresponding send span not found");
                    }
                }
            }

            if (individualMessageSpans.Any())
            {
                spanLinksFoundOnBatchSend.Should().BeTrue(
                    "Batch send span should have span links to individual message spans, " +
                    "indicating that batch span linking is working correctly.");
            }

            (spanLinksFoundOnReceive || spanLinksFoundOnBatchSend).Should().BeTrue(
                "At least one span should have span links, " +
                "indicating that span linking is working correctly for message contexts.");
        }
    }
}
