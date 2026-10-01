// <copyright file="DnsClientTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
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

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
    public class DnsClientTests : TracingIntegrationTest
    {
        public DnsClientTests(ITestOutputHelper output)
            : base("DnsClient", output)
        {
            SetServiceVersion("1.0.0");
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.IsDnsClient(metadataSchemaVersion);

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitTraces(
            [PackageVersionData(nameof(PackageVersions.DnsClient))] string packageVersion,
            [MetadataSchemaVersionData] string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-dns" : EnvironmentHelper.FullSampleName;

            // Each sync/async pass: 12 query API spans, 7 from extensions (including fan-out),
            // 2 transport, 2 cache, and 7 error/recovery/empty-response spans.
            // Finally, one cancelled query. QueryCache itself produces no span.
            const int expectedSpanCount = 61;

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);

            var spans = await agent.WaitForSpansAsync(expectedSpanCount);
            var dnsSpans = spans
                          .Where(span => string.Equals(span.GetTag("component"), "DnsClient", StringComparison.OrdinalIgnoreCase))
                          .ToList();

            using var s = new AssertionScope();
            dnsSpans.Count.Should().Be(expectedSpanCount);

            ValidateIntegrationSpans(dnsSpans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);

            foreach (var span in dnsSpans)
            {
                span.Name.Should().Be("dns.query");
                span.Type.Should().Be("dns");
                span.Tags[Tags.SpanKind].Should().Be(SpanKinds.Client);
                span.Tags.Should().ContainKey("dns.question.name");
                span.Tags.Should().ContainKey("dns.question.type");

                span.GetTag(Tags.OutHost).Should().Be("127.0.0.1");
                span.GetTag(Tags.NetworkDestinationPort).Should().NotBeNullOrEmpty();

                if (metadataSchemaVersion == "v1")
                {
                    span.GetTag(Tags.PeerService).Should().Be(span.GetTag(Tags.OutHost));
                    span.GetTag(Tags.PeerServiceSource).Should().Be(Tags.OutHost);
                }
                else
                {
                    // The v0 schema never emits peer.service tags.
                    span.Tags.Should().NotContainKey("peer.service");
                    span.Tags.Should().NotContainKey("_dd.peer.service.source");
                }

                span.Tags.Should().NotContainKey(Tags.PeerServiceRemappedFrom);
            }

            var errorSpans = dnsSpans.Where(span => span.Error == 1).ToList();
            errorSpans.Should().HaveCount(9); // NXDOMAIN, SERVFAIL, throwing NXDOMAIN, timeout (sync/async), cancellation.
            foreach (var errorSpan in errorSpans)
            {
                errorSpan.GetTag(Tags.ErrorType).Should().Be("DnsClient.DnsResponseException");
                errorSpan.GetTag(Tags.ErrorMsg).Should().NotBeNullOrEmpty();
                if (errorSpan.GetTag(Tags.DnsResponseCode) is null)
                {
                    errorSpan.GetTag(Tags.ErrorStack).Should().NotBeNullOrEmpty();
                }
            }

            // Keep server roles distinguishable after scrubbing ephemeral ports.
            var primaryPort = dnsSpans.First(span => span.Resource == "a.sync.dns.test.").GetTag(Tags.NetworkDestinationPort);
            var failoverSpans = dnsSpans.Where(span => span.Resource.StartsWith("failover.", StringComparison.Ordinal)).ToList();
            failoverSpans.Should().HaveCount(2);
            var secondaryPort = failoverSpans.First().GetTag(Tags.NetworkDestinationPort);
            secondaryPort.Should().NotBe(primaryPort);
            foreach (var span in dnsSpans)
            {
                span.GetTag(Tags.NetworkDestinationPort).Should().Be(failoverSpans.Contains(span) ? secondaryPort : primaryPort);
            }

            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.DnsClient);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddSimpleScrubber($"network.destination.port: {primaryPort}", "network.destination.port: primary-port");
            settings.AddSimpleScrubber($"network.destination.port: {secondaryPort}", "network.destination.port: secondary-port");
            settings.AddRegexScrubber(new Regex(@"Query \d+ =>"), "Query <id> =>");
            // Exception frames/inner exceptions differ across DnsClient versions and runtimes.
            // Their presence is asserted above; retain error type and message in the snapshot.
            settings.ModifySerialization(serialization => serialization.MemberConverter<MockSpan, Dictionary<string, string>>(span => span.Tags, ScrubTags));
            await VerifyHelper.VerifySpans(dnsSpans, settings, values => values.OrderBy(span => span.Resource).ThenBy(span => span.GetTag(Tags.DnsQuestionType)).ThenBy(span => span.Start))
                              .DisableRequireUniquePrefix()
                              .UseFileName($"{nameof(DnsClientTests)}.Schema{metadataSchemaVersion.ToUpperInvariant()}");
        }

        private static Dictionary<string, string> ScrubTags(MockSpan span, Dictionary<string, string> tags)
        {
            var scrubbed = VerifyHelper.ScrubStringTags(span, tags);
            if (scrubbed.ContainsKey(Tags.ErrorStack))
            {
                scrubbed[Tags.ErrorStack] = "<stack trace>";
            }

            return scrubbed;
        }
    }
}
