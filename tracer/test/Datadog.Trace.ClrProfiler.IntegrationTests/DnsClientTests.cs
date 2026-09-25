// <copyright file="DnsClientTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
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

            // The sample issues at least this many DNS lookups (sync + async + error paths).
            // GetHostEntry may fan out into multiple lookups, so treat this as a lower bound.
            const int minExpectedSpanCount = 12;

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);

            var spans = await agent.WaitForSpansAsync(minExpectedSpanCount);
            var dnsSpans = spans
                          .Where(span => string.Equals(span.GetTag("component"), "DnsClient", StringComparison.OrdinalIgnoreCase))
                          .ToList();

            using var s = new AssertionScope();
            dnsSpans.Count.Should().BeGreaterOrEqualTo(minExpectedSpanCount);

            ValidateIntegrationSpans(dnsSpans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);

            foreach (var span in dnsSpans)
            {
                span.Name.Should().Be("dns.query");
                span.Type.Should().Be("dns");
                span.Tags[Tags.SpanKind].Should().Be(SpanKinds.Client);
                span.Tags.Should().ContainKey("dns.question.name");
                span.Tags.Should().ContainKey("dns.question.type");

                // The instrumentation always captures the target name server as out.host,
                // which is the precursor attribute used to derive peer.service.
                span.Tags.Should().ContainKey("out.host");

                if (metadataSchemaVersion == "v1")
                {
                    // With the v1 schema, peer.service must be derived from the out.host precursor.
                    var outHost = span.GetTag("out.host");
                    span.GetTag("peer.service").Should().Be(outHost);
                    span.GetTag("_dd.peer.service.source").Should().Be("out.host");
                }
                else
                {
                    // The v0 schema never emits peer.service tags.
                    span.Tags.Should().NotContainKey("peer.service");
                    span.Tags.Should().NotContainKey("_dd.peer.service.source");
                }
            }

            // At least one span should represent a query for records of datadoghq.com.
            dnsSpans.Should().Contain(span => span.GetTag("dns.question.name") != null && span.GetTag("dns.question.name").Contains("datadoghq.com"));

            // The sample deliberately triggers failures (NXDOMAIN with ThrowDnsErrors, unreachable server),
            // so at least one span must be marked as an error with error metadata attached.
            var errorSpans = dnsSpans.Where(span => span.Error == 1).ToList();
            errorSpans.Should().NotBeEmpty();
            foreach (var errorSpan in errorSpans)
            {
                errorSpan.Tags.Should().ContainKey(Tags.ErrorType);
                errorSpan.Tags.Should().ContainKey(Tags.ErrorMsg);
            }

            // The sample also issues an NXDOMAIN lookup with the default settings (ThrowDnsErrors=false),
            // where the failure surfaces as a non-success response code with no exception thrown.
            // That path must still be marked as an error carrying the response code.
            errorSpans.Should().Contain(span => span.GetTag("dns.response.code") == "NotExistentDomain");

            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.DnsClient);
        }
    }
}
