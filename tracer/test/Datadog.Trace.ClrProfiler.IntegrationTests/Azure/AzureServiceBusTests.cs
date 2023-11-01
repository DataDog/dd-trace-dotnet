// <copyright file="AzureServiceBusTests.cs" company="Datadog">
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
    [UsesVerify]
    public class AzureServiceBusTests : TracingIntegrationTest
    {
        public AzureServiceBusTests(ITestOutputHelper output)
            : base("AzureServiceBus", output)
        {
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in new string[] { string.Empty }
               from metadataSchemaVersion in new[] { "v0", "v1" }
               select new string[] { packageVersionArray, metadataSchemaVersion };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.Tags["span.kind"] switch
        {
            SpanKinds.Consumer => span.IsAzureServiceBusInbound(metadataSchemaVersion),
            SpanKinds.Producer => span.IsAzureServiceBusOutbound(metadataSchemaVersion),
            SpanKinds.Client => span.IsAzureServiceBusRequest(metadataSchemaVersion),
            _ => throw new ArgumentException($"span.Tags[\"span.kind\"] is not a supported value for the AWS SQS integration: {span.Tags["span.kind"]}", nameof(span)),
        };

        [SkippableTheory(Skip = "We are unable to test all the features of Azure Service Bus with an emulator. For now, run only locally with a connection string to a live Azure Service Bus namespace")]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTraces(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "true");

            // If you want to use a custom connection string, set it here
            // SetEnvironmentVariable("ASB_CONNECTION_STRING", null);

            using (var telemetry = this.ConfigureTelemetry())
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                const int expectedProcessorSpanCount = 91;
                agent.SpanFilters.Add(s =>
                    (s.Tags.TryGetValue("otel.library.name", out var value) && value == "Samples.AzureServiceBus")
                    || (s.Tags.TryGetValue("messaging.system", out value) && value == "servicebus")); // Exclude the Admin requests
                var spans = agent.WaitForSpans(expectedProcessorSpanCount);

                using var s = new AssertionScope();
                spans.Count().Should().Be(expectedProcessorSpanCount);

                var serviceBusSpans = spans.Where(s => s.Tags["span.kind"] != "internal");
                ValidateIntegrationSpans(serviceBusSpans, metadataSchemaVersion, expectedServiceName: "Samples.AzureServiceBus", isExternalSpan: false);

                var filename = $"{nameof(AzureServiceBusTests)}.Schema{metadataSchemaVersion.ToUpper()}";

                var settings = VerifyHelper.GetSpanVerifierSettings();
                settings.AddRegexScrubber(new Regex(@"net.peer.name: [a-zA-Z0-9-]+.servicebus.windows.net"), "net.peer.name: localhost");
                settings.AddRegexScrubber(new Regex(@"peer.service: [a-zA-Z0-9-]+.servicebus.windows.net"), "peer.service: localhost");

                await VerifyHelper.VerifySpans(spans, settings, OrderSpans)
                                  .UseFileName(filename)
                                  .DisableRequireUniquePrefix();

                telemetry.AssertIntegrationEnabled(IntegrationId.OpenTelemetry);
            }
        }

        private static IOrderedEnumerable<MockSpan> OrderSpans(IReadOnlyCollection<MockSpan> spans)
            => spans
                .OrderBy(x => x.Start)
                .ThenBy(x => VerifyHelper.GetRootSpanResourceName(x, spans))
                .ThenBy(x => VerifyHelper.GetSpanDepth(x, spans))
                .ThenBy(x => x.Duration);
    }
}
