// <copyright file="AzureServiceBusAPMTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Azure
{
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "2")]
    [Trait("Category", "ArmUnsupported")]
    [UsesVerify]
    public class AzureServiceBusAPMTests : TracingIntegrationTest
    {
        public AzureServiceBusAPMTests(ITestOutputHelper output)
            : base("AzureServiceBus.APM", output)
        {
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.AzureServiceBusAPM
               from metadataSchemaVersion in new[] { "v0", "v1" }
               select new[] { packageVersionArray[0], metadataSchemaVersion };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.Tags["span.kind"] switch
        {
            SpanKinds.Consumer => span.IsAzureServiceBusInboundAPM(metadataSchemaVersion),
            SpanKinds.Producer when span.Name == "azure_servicebus.create" => span.IsAzureServiceBusCreateAPM(metadataSchemaVersion),
            SpanKinds.Producer => span.IsAzureServiceBusOutboundAPM(metadataSchemaVersion),
            _ => throw new ArgumentException($"span.Tags[\"span.kind\"] is not a supported value for the Azure Service Bus integration: {span.Tags["span.kind"]}", nameof(span)),
        };

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task TestSendMessagesAsyncIntegration(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("DD_TRACE_AZURESERVICEBUS_ENABLED", "true");
            SetEnvironmentVariable("ASB_TEST_MODE", "SendMessages");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(2, timeoutInMilliseconds: 30000);

                using var s = new AssertionScope();

                var sendSpans = spans.Where(span => span.Name == "azure_servicebus.send");

                Output.WriteLine($"Datadog Service Bus send spans found: {sendSpans.Count()}");

                if (sendSpans.Any())
                {
                    foreach (var span in sendSpans)
                    {
                        var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                        result.Success.Should().BeTrue($"Span validation failed: {result}");
                    }
                }
                else
                {
                    var diagnosticInfo = $"No Datadog Service Bus spans found. " +
                        $"Total spans: {spans.Count()}, " +
                        $"Operations: [{string.Join(", ", spans.Select(s => s.Name).Distinct())}]";
                    Assert.Fail(diagnosticInfo);
                }
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task TestReceiveMessagesAsyncIntegration(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("DD_TRACE_AZURESERVICEBUS_ENABLED", "true");
            SetEnvironmentVariable("ASB_TEST_MODE", "ReceiveMessages");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(2, timeoutInMilliseconds: 30000);

                using var s = new AssertionScope();

                var receiveSpans = spans.Where(span => span.Name.StartsWith("azure_servicebus.receive")).ToList();
                var sendSpans = spans.Where(span => span.Name.StartsWith("azure_servicebus.send")).ToList();

                Output.WriteLine($"Service Bus spans found: {receiveSpans.Count}");

                receiveSpans.Should().NotBeEmpty("Expected to find consumer spans for message receiving operations");

                foreach (var span in receiveSpans)
                {
                    var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"Receive span validation failed: {result}");
                }

                ValidateSpanLinks(sendSpans, receiveSpans, spans);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task TestReceiveMessagesAsyncIntegrationMultiple(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("DD_TRACE_AZURESERVICEBUS_ENABLED", "true");
            SetEnvironmentVariable("ASB_TEST_MODE", "ReceiveMessagesMultiple");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(2, timeoutInMilliseconds: 30000);

                using var s = new AssertionScope();

                var receiveSpans = spans.Where(span => span.Name.StartsWith("azure_servicebus.receive")).ToList();
                var sendSpans = spans.Where(span => span.Name.StartsWith("azure_servicebus.send")).ToList();

                Output.WriteLine($"Service Bus spans found: {receiveSpans.Count}");

                receiveSpans.Should().NotBeEmpty("Expected to find consumer spans for message receiving operations");

                foreach (var span in receiveSpans)
                {
                    var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"Receive span validation failed: {result}");
                }

                ValidateSpanLinks(sendSpans, receiveSpans, spans);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task TestServiceBusMessageBatchIntegration(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("DD_TRACE_AZURESERVICEBUS_ENABLED", "true");
            SetEnvironmentVariable("DD_TRACE_AZURE_SERVICEBUS_BATCH_LINKS_ENABLED", "true");
            SetEnvironmentVariable("ASB_TEST_MODE", "TestServiceBusMessageBatch");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(5, timeoutInMilliseconds: 30000);

                using var s = new AssertionScope();
                Output.WriteLine($"TOTAL SPANS FOUND: {spans.Count}");

                var createSpans = spans.Where(span => span.Name == "azure_servicebus.create").ToList();
                var sendSpans = spans.Where(span => span.Name == "azure_servicebus.send").ToList();
                var receiveSpans = spans.Where(span => span.Name == "azure_servicebus.receive").ToList();

                Output.WriteLine($"Create spans found: {createSpans.Count}");
                Output.WriteLine($"Send spans found: {sendSpans.Count}");
                Output.WriteLine($"Receive spans found: {receiveSpans.Count}");

                createSpans.Should().HaveCount(3, "Expected 3 TryAddMessage spans with azure_servicebus.create operation");
                sendSpans.Should().HaveCount(1, "Expected 1 SendMessagesAsync span with azure_servicebus.send operation");
                receiveSpans.Should().HaveCount(1, "Expected 1 ReceiveMessagesAsync span");

                var individualMessageSpans = createSpans.Where(s => s.Resource == "samples-azureservicebus-queue").ToList();
                individualMessageSpans.Should().HaveCount(3, "Expected 3 individual message spans from TryAddMessage operations");

                var batchSendSpans = sendSpans.Where(s => s.Resource == "samples-azureservicebus-queue").ToList();
                batchSendSpans.Should().HaveCount(1, "Expected 1 batch send span from SendMessagesAsync operation");

                foreach (var createSpan in createSpans)
                {
                    var result = ValidateIntegrationSpan(createSpan, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"Create span validation failed: {result}");
                }

                foreach (var sendSpan in sendSpans)
                {
                    var result = ValidateIntegrationSpan(sendSpan, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"Send span validation failed: {result}");
                }

                foreach (var receiveSpan in receiveSpans)
                {
                    var result = ValidateIntegrationSpan(receiveSpan, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"Receive span validation failed: {result}");
                }

                var allProducerSpans = createSpans.Concat(sendSpans).ToList();
                ValidateSpanLinks(allProducerSpans, receiveSpans, spans);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task TestServiceBusMessageBatchIntegrationWithoutBatchLinks(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("DD_TRACE_AZURESERVICEBUS_ENABLED", "true");
            SetEnvironmentVariable("DD_TRACE_AZURE_SERVICEBUS_BATCH_LINKS_ENABLED", "false");
            SetEnvironmentVariable("ASB_TEST_MODE", "TestServiceBusMessageBatch");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(2, timeoutInMilliseconds: 30000);

                using var s = new AssertionScope();

                Output.WriteLine($"TOTAL SPANS FOUND: {spans.Count}");
                foreach (var span in spans)
                {
                    Output.WriteLine($"  Span: Name={span.Name}, Resource={span.Resource}, Service={span.Service}, Tags=[{string.Join(", ", span.Tags.Select(kvp => $"{kvp.Key}={kvp.Value}"))}]");
                }

                var sendSpans = spans.Where(span => span.Name == "azure_servicebus.send").ToList();
                var receiveSpans = spans.Where(span => span.Name == "azure_servicebus.receive").ToList();

                Output.WriteLine($"Send spans found: {sendSpans.Count}");
                Output.WriteLine($"Receive spans found: {receiveSpans.Count}");

                sendSpans.Should().HaveCount(1, "Expected 1 SendMessagesAsync span (no individual TryAddMessage spans when batch links disabled)");
                receiveSpans.Should().HaveCount(1, "Expected 1 ReceiveMessagesAsync span");

                var createSpans = spans.Where(span => span.Name == "azure_servicebus.create").ToList();
                createSpans.Should().BeEmpty("Expected no individual message spans (azure_servicebus.create) when batch links disabled");

                foreach (var span in spans)
                {
                    var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"Service Bus span validation failed: {result}");
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
        public async Task TestScheduleMessagesAsyncIntegration(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("DD_TRACE_AZURESERVICEBUS_ENABLED", "true");
            SetEnvironmentVariable("ASB_TEST_MODE", "ScheduleMessages");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(1, timeoutInMilliseconds: 30000);

                using var s = new AssertionScope();

                var scheduleSpans = spans.Where(span => span.Name == "azure_servicebus.send" &&
                                                        span.Tags.ContainsKey("messaging.operation") &&
                                                        span.Tags["messaging.operation"] == "send").ToList();

                Output.WriteLine($"Datadog Service Bus schedule spans found: {scheduleSpans.Count}");

                if (scheduleSpans.Any())
                {
                    scheduleSpans.Should().HaveCount(1, "Expected exactly 1 span from ScheduleMessagesAsync");

                    foreach (var span in scheduleSpans)
                    {
                        var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                        result.Success.Should().BeTrue($"Schedule span validation failed: {result}");
                    }
                }
                else
                {
                    var diagnosticInfo = $"No Datadog Service Bus schedule spans found. " +
                        $"Total spans: {spans.Count}, " +
                        $"Operations: [{string.Join(", ", spans.Select(s => s.Name).Distinct())}]";
                    Assert.Fail(diagnosticInfo);
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

            var batchSendSpans = sendSpans.Where(s => s.Name == "azure_servicebus.send").ToList();
            var individualMessageSpans = sendSpans.Where(s => s.Name == "azure_servicebus.create").ToList();

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

        private IOrderedEnumerable<MockSpan> OrderSpans(IReadOnlyCollection<MockSpan> spans)
            => spans
                .OrderBy(x => x.Start)
                .ThenBy(x => VerifyHelper.GetRootSpanResourceName(x, spans))
                .ThenBy(x => VerifyHelper.GetSpanDepth(x, spans))
                .ThenBy(x => x.Duration);
    }
}
