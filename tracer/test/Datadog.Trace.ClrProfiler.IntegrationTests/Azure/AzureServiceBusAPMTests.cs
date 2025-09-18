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
