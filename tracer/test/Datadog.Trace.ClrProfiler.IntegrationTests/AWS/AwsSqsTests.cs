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
    [Trait("DockerGroup", "2")]
    [UsesVerify]
    public class AwsSqsTests : TracingIntegrationTest
    {
        public AwsSqsTests(ITestOutputHelper output)
            : base("AWS.SQS", output)
        {
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.AwsSqs
               select new[] { packageVersionArray[0] };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.Tags["span.kind"] switch
        {
            SpanKinds.Consumer => span.IsAwsSqsInbound(),
            SpanKinds.Producer => span.IsAwsSqsOutbound(),
            SpanKinds.Client => span.IsAwsSqsRequest(),
            _ => throw new ArgumentException($"span.Tags[\"span.kind\"] is not a supported value for the AWS SQS integration: {span.Tags["span.kind"]}", nameof(span)),
        };

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTraces(string packageVersion)
        {
            const string metadataSchemaVersion = "v0";
            var clientSpanServiceName = $"{EnvironmentHelper.FullSampleName}-aws-sqs";

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
                var spans = await agent.WaitForSpansAsync(expectedCount);
                var sqsSpans = spans.Where(
                    span => span.Tags.TryGetValue("component", out var component) && component == "aws-sdk");

                sqsSpans.Should().NotBeEmpty();
                ValidateIntegrationSpans(sqsSpans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan: true);

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
                // V4 uses the sockets handler by default where possible instead of the httpclienthandler
                settings.AddSimpleScrubber("http-client-handler-type: System.Net.Http.SocketsHttpHandler", "http-client-handler-type: System.Net.Http.HttpClientHandler");

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

                await telemetry.AssertIntegrationEnabledAsync(IntegrationId.AwsSqs);

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
