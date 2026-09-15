// <copyright file="AzureEventGridNamespacesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Azure
{
    [Trait("Category", "ArmUnsupported")]
    [UsesVerify]
    public class AzureEventGridNamespacesTests : TracingIntegrationTest
    {
        private const string ExpectedOperationName = "azure_eventgrid.send";

        public AzureEventGridNamespacesTests(ITestOutputHelper output)
            : base("AzureEventGridNamespaces", output)
        {
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.AzureEventGridNamespaces
               from metadataSchemaVersion in new[] { "v0", "v1" }
               select new[] { packageVersionArray[0], metadataSchemaVersion };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.Tags["span.kind"] switch
            {
                SpanKinds.Producer => span.IsAzureEventGridOutbound(metadataSchemaVersion),
                _ => throw new ArgumentException($"span.Tags[\"span.kind\"] is not a supported value for the Azure Event Grid integration: {span.Tags["span.kind"]}", nameof(span)),
            };

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitCloudEvents(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var allSpans = await agent.WaitForSpansAsync(4, timeoutInMilliseconds: 5_000, operationName: ExpectedOperationName, returnAllOperations: true, failOnTimeout: false);
                var eventGridSpans = allSpans.Where(span => span.Name == ExpectedOperationName).ToList();

                using var scope = new AssertionScope();
                eventGridSpans.Should().HaveCount(4);
                var parentSpanIds = new HashSet<ulong>(eventGridSpans.Where(span => span.ParentId.HasValue).Select(span => span.ParentId.Value));
                var parentSpans = allSpans.Where(span => parentSpanIds.Contains(span.SpanId)).ToList();
                parentSpans.Should().HaveCount(4);

                foreach (var span in eventGridSpans)
                {
                    var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"Span validation failed: {result}");
                    span.Resource.Should().Be("eventgrid");
                }

                var spans = eventGridSpans.Concat(parentSpans).ToList();

                var settings = VerifyHelper.GetSpanVerifierSettings();
                settings.AddRegexScrubber(new Regex(@"network\.destination\.port: \d+", VerifyHelper.RegOptions), "network.destination.port: 00000");
                settings.UseFileName($"{nameof(AzureEventGridNamespacesTests)}.Schema{metadataSchemaVersion.ToUpper()}");
                settings.DisableRequireUniquePrefix();

                await VerifyHelper.VerifySpans(spans, settings);
            }
        }
    }
}
