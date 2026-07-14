// <copyright file="AzureEventGridNamespacesTests.cs" company="Datadog">
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
    [Trait("Category", "ArmUnsupported")]
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
               from testMode in new[] { "Send", "SendAsync", "SendBatch", "SendBatchAsync" }
               select new[] { packageVersionArray[0], metadataSchemaVersion, testMode };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.Tags["span.kind"] switch
            {
                SpanKinds.Producer => span.IsAzureEventGridOutbound(metadataSchemaVersion),
                _ => throw new ArgumentException($"span.Tags[\"span.kind\"] is not a supported value for the Azure Event Grid integration: {span.Tags["span.kind"]}", nameof(span)),
            };

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitCloudEvents(string packageVersion, string metadataSchemaVersion, string testMode)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("EVENTGRID_TEST_MODE", testMode);

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(1, timeoutInMilliseconds: 5_000, operationName: ExpectedOperationName, assertExpectedCount: false);

                using var scope = new AssertionScope();
                var span = spans.Should().ContainSingle($"Expected one producer span for EventGridSenderClient.{testMode}").Which;
                ValidateIntegrationSpan(span, metadataSchemaVersion).Success.Should().BeTrue();
                span.Tags["messaging.destination.name"].Should().Be("samples-eventgrid-topic");

                if (testMode.IndexOf("Batch", StringComparison.Ordinal) >= 0)
                {
                    span.Tags["messaging.batch.message_count"].Should().Be("3");
                }
            }
        }
    }
}
