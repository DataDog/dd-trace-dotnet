// <copyright file="AwsStepFunctionsTests.cs" company="Datadog">
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
    [Trait("DockerGroup", "2")]
    [UsesVerify]
    public class AwsStepFunctionsTests : TracingIntegrationTest
    {
        public AwsStepFunctionsTests(ITestOutputHelper output)
            : base("AWS.StepFunctions", output)
        {
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.AwsStepFunctions
               select new[] { packageVersionArray[0] };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.Tags["span.kind"] switch
        {
            SpanKinds.Consumer => span.IsAwsStepFunctionsInbound(),
            SpanKinds.Producer => span.IsAwsStepFunctionsOutbound(),
            SpanKinds.Client => span.IsAwsStepFunctionsRequest(),
            _ => throw new ArgumentException($"span.Tags[\"span.kind\"] is not a supported value for the AWS Step Functions integration: {span.Tags["span.kind"]}", nameof(span)),
        };

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTraces(string packageVersion)
        {
            const string metadataSchemaVersion = "v0";
            var clientSpanServiceName = $"{EnvironmentHelper.FullSampleName}-aws-stepfunctions";

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
                var spans = await agent.WaitForSpansAsync(expectedCount);
                var stepFunctionsSpans = spans.Where(span => span.Tags.TryGetValue("component", out var component) && component == "aws-sdk");

                stepFunctionsSpans.Should().NotBeEmpty();
                ValidateIntegrationSpans(stepFunctionsSpans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan: true);

                var host = Environment.GetEnvironmentVariable("AWS_SDK_HOST");

                var settings = VerifyHelper.GetSpanVerifierSettings();

                // Default version is 3.7.*
                var snapshotSuffix = string.IsNullOrEmpty(packageVersion) ? string.Empty :
                    new Version(packageVersion) switch
                    {
                        { Major: 3, Minor: >= 7 } => string.Empty, // Post 3.7.0
                        _ => "_pre3_7_0"  // Pre 3.7.0

                    };

                settings.UseFileName($"{nameof(AwsStepFunctionsTests)}.{frameworkName}.Schema{metadataSchemaVersion.ToUpper()}{snapshotSuffix}");
                settings.AddSimpleScrubber("out.host: localhost", "out.host: aws_stepfunctions");
                settings.AddSimpleScrubber("out.host: localstack", "out.host: aws_stepfunctions");
                settings.AddSimpleScrubber("out.host: localstack_arm64", "out.host: aws_stepfunctions");
                settings.AddSimpleScrubber("peer.service: localhost", "peer.service: aws_stepfunctions");
                settings.AddSimpleScrubber("peer.service: localstack", "peer.service: aws_stepfunctions");
                settings.AddSimpleScrubber("peer.service: localstack_arm64", "peer.service: aws_stepfunctions");
                // V4 uses the sockets handler by default where possible instead of the httpclienthandler
                settings.AddSimpleScrubber("http-client-handler-type: System.Net.Http.SocketsHttpHandler", "http-client-handler-type: System.Net.Http.HttpClientHandler");
                if (!string.IsNullOrWhiteSpace(host))
                {
                    settings.AddSimpleScrubber(host, "localhost:00000");
                }

                settings.DisableRequireUniquePrefix();

                await VerifyHelper.VerifySpans(spans, settings);

                await telemetry.AssertIntegrationEnabledAsync(IntegrationId.AwsStepFunctions);
            }
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        public async Task IntegrationDisabled()
        {
            const string expectedOperationName = "aws.stepfunctions.request";

            SetEnvironmentVariable($"DD_TRACE_{nameof(IntegrationId.AwsStepFunctions)}_ENABLED", "false");

            using var telemetry = this.ConfigureTelemetry();
            var packageVersion = PackageVersions.AwsStepFunctions.First()[0] as string;
            using var agent = EnvironmentHelper.GetMockAgent();
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(1, returnAllOperations: true);

                Assert.NotEmpty(spans);
                spans.Where(s => s.Name.Equals(expectedOperationName)).Should().BeEmpty();
                await telemetry.AssertIntegrationDisabledAsync(IntegrationId.AwsStepFunctions);
            }
        }
    }
}
