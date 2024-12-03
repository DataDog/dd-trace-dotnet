// <copyright file="AwsEventBridgeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AWS
{
    [Trait("RequiresDockerDependency", "true")]
    [UsesVerify]
    public class AwsEventBridgeTests : TracingIntegrationTest
    {
        public AwsEventBridgeTests(ITestOutputHelper output)
            : base("AWS.EventBridge", output)
        {
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.AwsEventBridge
               from metadataSchemaVersion in new[] { "v0", "v1" }
               select new[] { packageVersionArray[0], metadataSchemaVersion };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.Tags["span.kind"] switch
        {
            SpanKinds.Consumer => span.IsAwsEventBridgeInbound(metadataSchemaVersion),
            SpanKinds.Producer => span.IsAwsEventBridgeOutbound(metadataSchemaVersion),
            SpanKinds.Client => span.IsAwsEventBridgeRequest(metadataSchemaVersion),
            _ => throw new ArgumentException($"span.Tags[\"span.kind\"] is not a supported value for the AWS EventBridge integration: {span.Tags["span.kind"]}", nameof(span)),
        };

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTraces(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-aws-eventbridge" : EnvironmentHelper.FullSampleName;

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
#if NETFRAMEWORK
                var expectedCount = 8;
                var frameworkName = "NetFramework";
#else
                var expectedCount = 4;
                var frameworkName = "NetCore";
#endif
                var spans = agent.WaitForSpans(expectedCount);
                var eventBridgeSpans = spans.Where(span => span.Tags.TryGetValue("component", out var component) && component == "aws-sdk");

                eventBridgeSpans.Should().NotBeEmpty();
                ValidateIntegrationSpans(eventBridgeSpans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);

                var host = Environment.GetEnvironmentVariable("AWS_SDK_HOST");

                var settings = VerifyHelper.GetSpanVerifierSettings();

                settings.UseFileName($"{nameof(AwsEventBridgeTests)}.{frameworkName}.Schema{metadataSchemaVersion.ToUpper()}");
                settings.AddSimpleScrubber("out.host: localhost", "out.host: aws_eventbridge");
                settings.AddSimpleScrubber("out.host: localstack", "out.host: aws_eventbridge");
                settings.AddSimpleScrubber("out.host: localstack_arm64", "out.host: aws_eventbridge");
                settings.AddSimpleScrubber("peer.service: localhost", "peer.service: aws_eventbridge");
                settings.AddSimpleScrubber("peer.service: localstack", "peer.service: aws_eventbridge");
                settings.AddSimpleScrubber("peer.service: localstack_arm64", "peer.service: aws_eventbridge");
                if (!string.IsNullOrWhiteSpace(host))
                {
                    settings.AddSimpleScrubber(host, "localhost:00000");
                }

                settings.DisableRequireUniquePrefix();

                // Note: http.request spans are expected for the EventBridge APIs that don't have explicit support
                // (Only PutEvents and PutEventsAsync are supported right now)
                await VerifyHelper.VerifySpans(spans, settings);

                telemetry.AssertIntegrationEnabled(IntegrationId.AwsEventBridge);
            }
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        public async Task IntegrationDisabled()
        {
            const string expectedOperationName = "aws.eventbridge.request";

            SetEnvironmentVariable($"DD_TRACE_{nameof(IntegrationId.AwsEventBridge)}_ENABLED", "false");

            using var telemetry = this.ConfigureTelemetry();
            string packageVersion = PackageVersions.AwsEventBridge.First()[0] as string;
            using var agent = EnvironmentHelper.GetMockAgent();
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(1, returnAllOperations: true);

                Assert.NotEmpty(spans);
                Assert.Empty(spans.Where(s => s.Name.Equals(expectedOperationName)));
                telemetry.AssertIntegrationDisabled(IntegrationId.AwsEventBridge);
            }
        }
    }
}
