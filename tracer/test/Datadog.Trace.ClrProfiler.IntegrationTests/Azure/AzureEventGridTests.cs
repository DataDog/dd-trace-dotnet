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
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Azure
{
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "2")]
    [Trait("Category", "ArmUnsupported")]
    public class AzureEventGridTests : TracingIntegrationTest
    {
        private const string ExpectedOperationName = "azure_eventgrid.send";

        public AzureEventGridTests(ITestOutputHelper output)
            : base("AzureEventGrid", output)
        {
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.AzureEventGrid
               from metadataSchemaVersion in new[] { "v0", "v1" }
               from testMode in new[] { "SendEventGridEvent", "SendEventGridEventAsync", "SendEventGridEvents", "SendEventGridEventsAsync", "SendCloudEvent", "SendCloudEventAsync", "SendCloudEvents", "SendCloudEventsAsync" }
               select new[] { packageVersionArray[0], metadataSchemaVersion, testMode };

        // Partner channel overloads were introduced in Azure.Messaging.EventGrid 4.11.0.
        public static IEnumerable<object[]> GetPartnerChannelEnabledConfig()
            => from packageVersionArray in PackageVersions.AzureEventGrid
               let packageVersion = (string)packageVersionArray[0]
               where string.IsNullOrEmpty(packageVersion) || new Version(packageVersion) >= new Version(4, 11, 0)
               from metadataSchemaVersion in new[] { "v0", "v1" }
               from testMode in new[] { "SendCloudEventToChannel", "SendCloudEventsToChannel", "SendCloudEventToChannelAsync", "SendCloudEventsToChannelAsync" }
               select new[] { packageVersion, metadataSchemaVersion, testMode };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.Tags["span.kind"] switch
            {
                SpanKinds.Producer => span.IsAzureEventGridOutbound(metadataSchemaVersion),
                _ => throw new ArgumentException($"span.Tags[\"span.kind\"] is not a supported value for the Azure EventGrid integration: {span.Tags["span.kind"]}", nameof(span)),
            };

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [MemberData(nameof(GetPartnerChannelEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitEvents(string packageVersion, string metadataSchemaVersion, string testMode)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("EVENTGRID_TEST_MODE", testMode);

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(1, timeoutInMilliseconds: 5_000, operationName: ExpectedOperationName, assertExpectedCount: false);

                using var s = new AssertionScope();
                var span = spans.Should().ContainSingle($"Expected one producer span for azure_eventgrid.send ({testMode})").Which;
                var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                result.Success.Should().BeTrue($"Span validation failed: {result}");
            }
        }
    }
}
