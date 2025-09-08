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
        public async Task TestReceiveMessagesAsyncIntegrationWithParent(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("DD_TRACE_AZURESERVICEBUS_ENABLED", "true");

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

                ValidateContextPropagation(sendSpans, receiveSpans, spans);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task TestReceiveMessagesAsyncIntegrationWithSpanLinks(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("DD_TRACE_AZURESERVICEBUS_ENABLED", "true");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(6, timeoutInMilliseconds: 30000);

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

        private static void ValidateContextPropagation(
            IList<MockSpan> sendSpans,
            IList<MockSpan> receiveSpans,
            IReadOnlyCollection<MockSpan> allSpans)
        {
            sendSpans.Should().NotBeEmpty("Need producer spans to validate context propagation");
            receiveSpans.Should().NotBeEmpty("Need consumer spans to validate context propagation");

            var sendTraceIds = sendSpans.Select(s => s.TraceId).Distinct().ToList();
            var receiveTraceIds = receiveSpans.Select(s => s.TraceId).Distinct().ToList();

            var sharedTraceIds = sendTraceIds.Intersect(receiveTraceIds).ToList();
            sharedTraceIds.Should().NotBeEmpty(
                "Consumer spans should share trace IDs with producer spans, indicating successful context propagation. " +
                $"Send trace IDs: [{string.Join(", ", sendTraceIds)}], " +
                $"Receive trace IDs: [{string.Join(", ", receiveTraceIds)}]");

            var contextPropagationFound = false;

            foreach (var receiveSpan in receiveSpans)
            {
                var parentSendSpan = sendSpans.FirstOrDefault(s => s.SpanId == receiveSpan.ParentId && s.TraceId == receiveSpan.TraceId);
                if (parentSendSpan != null)
                {
                    contextPropagationFound = true;
                    break;
                }

                var sameTraceProducers = sendSpans.Where(s => s.TraceId == receiveSpan.TraceId).ToList();
                if (sameTraceProducers.Any())
                {
                    contextPropagationFound = true;
                    break;
                }
            }

            contextPropagationFound.Should().BeTrue(
                "At least one consumer span should be connected to a producer span through parent-child relationship or span links, " +
                "indicating that context propagation is working correctly through Service Bus messages.");
        }

        private static void ValidateSpanLinks(
            IList<MockSpan> sendSpans,
            IList<MockSpan> receiveSpans,
            IReadOnlyCollection<MockSpan> allSpans)
        {
            sendSpans.Should().NotBeEmpty("Need producer spans to validate span links");
            receiveSpans.Should().NotBeEmpty("Need consumer spans to validate span links");

            var spanLinksFound = false;

            foreach (var receiveSpan in receiveSpans)
            {
                if (receiveSpan.SpanLinks != null && receiveSpan.SpanLinks.Count > 0)
                {
                    spanLinksFound = true;

                    foreach (var link in receiveSpan.SpanLinks)
                    {
                        var linkedSendSpan = sendSpans.FirstOrDefault(s => s.TraceId == link.TraceIdLow && s.SpanId == link.SpanId);
                        linkedSendSpan.Should().NotBeNull(
                            $"Receive span {receiveSpan.SpanId} has link to span {link.SpanId} in trace {link.TraceIdLow}, " +
                            $"but corresponding send span not found");
                    }
                }
            }

            spanLinksFound.Should().BeTrue(
                "At least one consumer span should have span links to producer spans, " +
                "indicating that span linking is working correctly for heterogeneous message contexts.");
        }

        private IOrderedEnumerable<MockSpan> OrderSpans(IReadOnlyCollection<MockSpan> spans)
            => spans
                .OrderBy(x => x.Start)
                .ThenBy(x => VerifyHelper.GetRootSpanResourceName(x, spans))
                .ThenBy(x => VerifyHelper.GetSpanDepth(x, spans))
                .ThenBy(x => x.Duration);
    }
}
