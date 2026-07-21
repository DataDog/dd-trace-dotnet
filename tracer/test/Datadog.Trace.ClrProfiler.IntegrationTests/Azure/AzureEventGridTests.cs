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
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Azure
{
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "2")]
    [Trait("Category", "ArmUnsupported")]
    [UsesVerify]
    public class AzureEventGridTests : TracingIntegrationTest
    {
        private const string DefaultTopicEndpoint = "http://localhost:6500/samples-eventgrid-topic/api/events";
        private const string ExpectedOperationName = "azure_eventgrid.send";

        private static readonly Uri PublisherEndpoint =
            new(Environment.GetEnvironmentVariable("EVENTGRID_TOPIC_ENDPOINT") ?? DefaultTopicEndpoint);

        private static readonly string PublisherHost = PublisherEndpoint.Host;

        public AzureEventGridTests(ITestOutputHelper output)
            : base("AzureEventGrid", output)
        {
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.AzureEventGrid
               from metadataSchemaVersion in new[] { "v0", "v1" }
               select new[] { packageVersionArray[0], metadataSchemaVersion };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.Tags["span.kind"] switch
            {
                SpanKinds.Producer => span.IsAzureEventGridOutbound(metadataSchemaVersion),
                _ => throw new ArgumentException($"span.Tags[\"span.kind\"] is not a supported value for the Azure EventGrid integration: {span.Tags["span.kind"]}", nameof(span)),
            };

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitEvents(string packageVersion, string metadataSchemaVersion)
        {
            // Partner channel overloads were introduced in Azure.Messaging.EventGrid 4.11.0.
            var supportsPartnerChannels = string.IsNullOrEmpty(packageVersion) || new Version(packageVersion) >= new Version(4, 11, 0);
            var expectedSpanCount = supportsPartnerChannels ? 16 : 12;

            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var allSpans = await agent.WaitForSpansAsync(expectedSpanCount, timeoutInMilliseconds: 5_000, operationName: ExpectedOperationName, returnAllOperations: true, failOnTimeout: false);
                var eventGridSpans = allSpans.Where(span => span.Name == ExpectedOperationName).ToList();

                using var s = new AssertionScope();
                eventGridSpans.Should().HaveCount(expectedSpanCount);
                var parentSpanIds = new HashSet<ulong>(eventGridSpans.Where(span => span.ParentId.HasValue).Select(span => span.ParentId.Value));
                var parentSpans = allSpans.Where(span => parentSpanIds.Contains(span.SpanId)).ToList();
                parentSpans.Should().HaveCount(expectedSpanCount);

                foreach (var span in eventGridSpans)
                {
                    var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"Span validation failed: {result}");

                    span.Tags["network.destination.name"].Should().Be(PublisherHost);
                    span.Resource.Should().Be("eventgrid");
                }

                var spans = eventGridSpans.Concat(parentSpans).ToList();

                var settings = VerifyHelper.GetSpanVerifierSettings();
                settings.AddSimpleScrubber($"network.destination.name: {PublisherHost}", "network.destination.name: eventgrid");
                settings.AddSimpleScrubber($"network.destination.port: {PublisherEndpoint.Port}", "network.destination.port: 00000");
                settings.UseFileName($"{nameof(AzureEventGridTests)}.Schema{metadataSchemaVersion.ToUpper()}{(supportsPartnerChannels ? string.Empty : ".PrePartnerChannels")}");
                settings.DisableRequireUniquePrefix();

                await VerifyHelper.VerifySpans(spans, settings);
            }
        }
    }
}
