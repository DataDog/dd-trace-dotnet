// <copyright file="AwsSqsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AWS
{
    [Collection(nameof(AwsSqsTestsCollection))]
    [Trait("RequiresDockerDependency", "true")]
    [UsesVerify]
    public class AwsSqsTests : TracingIntegrationTest
    {
        public AwsSqsTests(ITestOutputHelper output)
            : base("AWS.SQS", output)
        {
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.AwsSqs
               from metadataSchemaVersion in new[] { "v0", "v1" }
               select new[] { packageVersionArray[0], metadataSchemaVersion };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.Tags["span.kind"] switch
        {
            SpanKinds.Consumer => span.IsAwsSqsInbound(metadataSchemaVersion),
            SpanKinds.Producer => span.IsAwsSqsOutbound(metadataSchemaVersion),
            SpanKinds.Client => span.IsAwsSqsRequest(metadataSchemaVersion),
            _ => throw new ArgumentException($"span.Tags[\"span.kind\"] is not a supported value for the AWS SQS integration: {span.Tags["span.kind"]}", nameof(span)),
        };

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTraces(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-aws-sqs" : EnvironmentHelper.FullSampleName;

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
#if NETFRAMEWORK
                var expectedCount = 56;
                var frameworkName = "NetFramework";
#else
                var expectedCount = 28;
                var frameworkName = "NetCore";
#endif
                var spans = agent.WaitForSpans(expectedCount);
                var sqsSpans = spans.Where(
                    span => span.Tags.TryGetValue("component", out var component) && component == "aws-sdk");

                sqsSpans.Should().NotBeEmpty();
                ValidateIntegrationSpans(sqsSpans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);

                var host = Environment.GetEnvironmentVariable("AWS_SDK_HOST");

                var settings = VerifyHelper.GetSpanVerifierSettings();
                var suffix = GetSnapshotSuffix(packageVersion);

                settings.UseFileName($"{nameof(AwsSqsTests)}.{frameworkName}.Schema{metadataSchemaVersion.ToUpper()}{suffix}");
                settings.AddSimpleScrubber("out.host: localhost", "out.host: aws_sqs");
                settings.AddSimpleScrubber("out.host: localstack", "out.host: aws_sqs");
                settings.AddSimpleScrubber("out.host: localstack_arm64", "out.host: aws_sqs");
                settings.AddSimpleScrubber("peer.service: localhost", "peer.service: aws_sqs");
                settings.AddSimpleScrubber("peer.service: localstack", "peer.service: aws_sqs");
                settings.AddSimpleScrubber("peer.service: localstack_arm64", "peer.service: aws_sqs");
                settings.AddSimpleScrubber("aws.queue.url: localstack_arm64", "peer.service: aws_sqs");
                settings.AddRegexScrubber(new Regex(@"sqs\..+\.localhost.*\.localstack.*\.cloud:4566"), "localhost:00000");

                if (!string.IsNullOrWhiteSpace(host))
                {
                    settings.AddSimpleScrubber(host, "localhost:00000");
                }

                settings.DisableRequireUniquePrefix();

                // Note: http.request spans are expected for the following SQS APIs that don't have explicit support
                // - ListQueues
                // - GetQueueUrl
                // - PurgeQueue
                await VerifyHelper.VerifySpans(spans, settings);

                telemetry.AssertIntegrationEnabled(IntegrationId.AwsSqs);

                static string GetSnapshotSuffix(string packageVersion)
                    => packageVersion switch
                    {
                        null or "" => ".pre3_7_300",
                        { } v when new Version(v) < new Version("3.7.300.6") => ".pre3_7_300",
                        _ => string.Empty
                    };
            }
        }

        [CollectionDefinition(nameof(AwsSqsTestsCollection), DisableParallelization = true)]
        public class AwsSqsTestsCollection
        {
            // Just an empty collection that's going to be used to prevent different SQS tests relying on the same "backend" (an SQS queue)
            // from running at the same time, which would cause unwanted interactions between them and make tests fail.
        }
    }
}
