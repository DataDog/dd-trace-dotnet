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
        public async Task TestSendEventGridEvent(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("EVENTGRID_TEST_MODE", "SendEventGridEvent");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(1, timeoutInMilliseconds: 5000, operationName: ExpectedOperationName, assertExpectedCount: false);

                using var s = new AssertionScope();

                spans.Should().HaveCountGreaterOrEqualTo(1, "Expected at least 1 producer span for azure_eventgrid.send (SendEvent with EventGridEvent)");

                foreach (var span in spans)
                {
                    var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"Span validation failed: {result}");
                }
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task TestSendEventGridEventAsync(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("EVENTGRID_TEST_MODE", "SendEventGridEventAsync");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(1, timeoutInMilliseconds: 5000, operationName: ExpectedOperationName, assertExpectedCount: false);

                using var s = new AssertionScope();

                spans.Should().HaveCountGreaterOrEqualTo(1, "Expected at least 1 producer span for azure_eventgrid.send (SendEventAsync with EventGridEvent)");

                foreach (var span in spans)
                {
                    var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"Span validation failed: {result}");
                }
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task TestSendEventGridEvents(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("EVENTGRID_TEST_MODE", "SendEventGridEvents");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(1, timeoutInMilliseconds: 5000, operationName: ExpectedOperationName, assertExpectedCount: false);

                using var s = new AssertionScope();

                spans.Should().HaveCountGreaterOrEqualTo(1, "Expected at least 1 producer span for azure_eventgrid.send (SendEvents with IEnumerable<EventGridEvent>)");

                foreach (var span in spans)
                {
                    var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"Span validation failed: {result}");
                }
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task TestSendEventGridEventsAsync(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("EVENTGRID_TEST_MODE", "SendEventGridEventsAsync");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(1, timeoutInMilliseconds: 5000, operationName: ExpectedOperationName, assertExpectedCount: false);

                using var s = new AssertionScope();

                spans.Should().HaveCountGreaterOrEqualTo(1, "Expected at least 1 producer span for azure_eventgrid.send (SendEventsAsync with IEnumerable<EventGridEvent>)");

                foreach (var span in spans)
                {
                    var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"Span validation failed: {result}");
                }
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task TestSendCloudEvent(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("EVENTGRID_TEST_MODE", "SendCloudEvent");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(1, timeoutInMilliseconds: 5000, operationName: ExpectedOperationName, assertExpectedCount: false);

                using var s = new AssertionScope();

                spans.Should().HaveCountGreaterOrEqualTo(1, "Expected at least 1 producer span for azure_eventgrid.send (SendEvent with CloudEvent)");

                foreach (var span in spans)
                {
                    var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"Span validation failed: {result}");
                }
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task TestSendCloudEventAsync(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("EVENTGRID_TEST_MODE", "SendCloudEventAsync");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(1, timeoutInMilliseconds: 5000, operationName: ExpectedOperationName, assertExpectedCount: false);

                using var s = new AssertionScope();

                spans.Should().HaveCountGreaterOrEqualTo(1, "Expected at least 1 producer span for azure_eventgrid.send (SendEventAsync with CloudEvent)");

                foreach (var span in spans)
                {
                    var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"Span validation failed: {result}");
                }
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task TestSendCloudEvents(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("EVENTGRID_TEST_MODE", "SendCloudEvents");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(1, timeoutInMilliseconds: 5000, operationName: ExpectedOperationName, assertExpectedCount: false);

                using var s = new AssertionScope();

                spans.Should().HaveCountGreaterOrEqualTo(1, "Expected at least 1 producer span for azure_eventgrid.send (SendEvents with IEnumerable<CloudEvent>)");

                foreach (var span in spans)
                {
                    var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"Span validation failed: {result}");
                }
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task TestSendCloudEventsAsync(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("EVENTGRID_TEST_MODE", "SendCloudEventsAsync");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(1, timeoutInMilliseconds: 5000, operationName: ExpectedOperationName, assertExpectedCount: false);

                using var s = new AssertionScope();

                spans.Should().HaveCountGreaterOrEqualTo(1, "Expected at least 1 producer span for azure_eventgrid.send (SendEventsAsync with IEnumerable<CloudEvent>)");

                foreach (var span in spans)
                {
                    var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                    result.Success.Should().BeTrue($"Span validation failed: {result}");
                }
            }
        }
    }
}
