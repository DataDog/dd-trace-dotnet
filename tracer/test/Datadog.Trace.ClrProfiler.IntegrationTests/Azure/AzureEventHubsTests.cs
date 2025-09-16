// <copyright file="AzureEventHubsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Azure
{
    [Trait("RequiresDockerDependency", "true")]
    public class AzureEventHubsTests : TracingIntegrationTest
    {
        public AzureEventHubsTests(ITestOutputHelper output)
            : base("AzureEventHubs", output)
        {
        }

        public static IEnumerable<object[]> GetEnabledConfig()
        {
            // Temporarily use a simple version array until PackageVersions.AzureEventHubs is generated
            var packageVersions = new[]
            {
                new object[] { string.Empty }, // Default version
            };

            return from packageVersionArray in packageVersions
                   from metadataSchemaVersion in new[] { "v0", "v1" }
                   select new[] { packageVersionArray[0], metadataSchemaVersion };
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.Tags["span.kind"] switch
        {
            SpanKinds.Consumer => span.IsAzureEventHubsInbound(metadataSchemaVersion),
            SpanKinds.Producer => span.IsAzureEventHubsOutbound(metadataSchemaVersion),
            _ => throw new ArgumentException($"span.Tags[\"span.kind\"] is not a supported value for the Azure EventHubs integration: {span.Tags["span.kind"]}", nameof(span)),
        };

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task TestSendBatchIntegration(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("DD_TRACE_AZUREEVENTHUBS_ENABLED", "true");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(4, timeoutInMilliseconds: 30000);

                using var s = new AssertionScope();

                // Look for producer spans (create + send operations)
                var createSpans = spans.Where(span => span.Name == "azure_eventhubs.create").ToList();
                var sendSpans = spans.Where(span => span.Name == "azure_eventhubs.send").ToList();

                Output.WriteLine($"EventHubs create spans found: {createSpans.Count}");
                Output.WriteLine($"EventHubs send spans found: {sendSpans.Count}");

                // We expect 3 create spans (one for each TryAdd) and 1 send span (batch send)
                createSpans.Should().HaveCount(3, "Expected 3 create spans for each event added to batch");
                sendSpans.Should().HaveCount(1, "Expected 1 send span for the batch send operation");

                // Validate create spans
                foreach (var span in createSpans)
                {
                    var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"Create span validation failed: {result}");
                }

                // Validate send spans
                foreach (var span in sendSpans)
                {
                    var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"Send span validation failed: {result}");
                }
            }
        }
    }
}
