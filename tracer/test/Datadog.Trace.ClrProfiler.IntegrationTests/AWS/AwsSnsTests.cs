// <copyright file="AwsSnsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
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
    public class AwsSnsTests : TracingIntegrationTest
    {
        private AmazonSQSClient _sqsClient;

        public AwsSnsTests(ITestOutputHelper output)
            : base("AWS.SimpleNotificationService", output)
        {
            _sqsClient = new AmazonSQSClient(new AmazonSQSConfig { ServiceURL = "http://localhost:4566" });
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.AwsSns
               from metadataSchemaVersion in new[] { "v0", "v1" }
               select new[] { packageVersionArray[0], metadataSchemaVersion };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsAwsSns(metadataSchemaVersion);

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTraces(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-aws-sns" : EnvironmentHelper.FullSampleName;

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
#if NETFRAMEWORK
                var expectedCount = 3;
                var frameworkName = "NetFramework";
#else
                var expectedCount = 3;
                var frameworkName = "NetCore";
#endif
                var spans = agent.WaitForSpans(expectedCount);
                // Poll the SQS queue for messages
                var receiveMessageRequest = new ReceiveMessageRequest
                {
                    QueueUrl = "http://localhost:4566/queue/MyQueue",  // replace with your queue URL
                    MaxNumberOfMessages = 10
                };
                var messages = await _sqsClient.ReceiveMessageAsync(receiveMessageRequest);

                // Assert the message attribute was added
                foreach (var message in messages.Messages)
                {
                    message.MessageAttributes.Should().ContainKey("TraceContext");
                    // Extract the datadog trace context as a b64 string, decode it and assert it's the same as for the sns spans
                    var traceContext = message.MessageAttributes["TraceContext"].StringValue;
                    // Add your decoding and assertion logic here
                }

                var snsSpans = spans.Where(span => span.Name == "sns.request");
                ValidateIntegrationSpans(snsSpans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);

                var host = Environment.GetEnvironmentVariable("AWS_SNS_HOST");

                var settings = VerifyHelper.GetSpanVerifierSettings();
                settings.UseFileName($"{nameof(AwsSnsTests)}.{frameworkName}.Schema{metadataSchemaVersion.ToUpper()}");
                settings.AddSimpleScrubber("peer.service: localhost", "peer.service: aws_sns");
                settings.AddSimpleScrubber("peer.service: aws_sns_arm64", "peer.service: aws_sns");
                if (!string.IsNullOrWhiteSpace(host))
                {
                    settings.AddSimpleScrubber(host, "localhost:00000");
                }

                settings.DisableRequireUniquePrefix();

                // Note: http.request spans are expected for the following SNS API's that don't have explicit support
                // - ListTopics
                // - GetTopicAttributes
                // - SetTopicAttributes
                await VerifyHelper.VerifySpans(spans, settings);

                telemetry.AssertIntegrationEnabled(IntegrationId.AwsSns);
            }
        }
    }
}
