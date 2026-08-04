// <copyright file="AwsS3Tests.cs" company="Datadog">
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
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "2")]
    [UsesVerify]
    public class AwsS3Tests : TracingIntegrationTest
    {
        public AwsS3Tests(ITestOutputHelper output)
            : base("AWS.S3", output)
        {
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.AwsS3
               select new[] { packageVersionArray[0] };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.Tags["span.kind"] switch
        {
            SpanKinds.Client => span.IsAwsS3Request(),
            _ => throw new ArgumentException($"span.Tags[\"span.kind\"] is not a supported value for the AWS S3 integration: {span.Tags["span.kind"]}", nameof(span)),
        };

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTraces(string packageVersion)
        {
            const string metadataSchemaVersion = "v0";
            var clientSpanServiceName = $"{EnvironmentHelper.FullSampleName}-aws-s3";

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
#if NETFRAMEWORK
                var expectedCount = 32;
                var frameworkName = "NetFramework";
#else
                var expectedCount = 16;
                var frameworkName = "NetCore";
#endif
                var spans = await agent.WaitForSpansAsync(expectedCount);
                spans.Count().Should().Be(expectedCount);
                var s3Spans = spans.Where(span => span.Tags.TryGetValue("component", out var component) && component == "aws-sdk");

                s3Spans.Should().NotBeEmpty();
                ValidateIntegrationSpans(s3Spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan: true);

                var host = Environment.GetEnvironmentVariable("AWS_SDK_HOST");

                var settings = VerifyHelper.GetSpanVerifierSettings();

                settings.UseFileName($"{nameof(AwsS3Tests)}.{frameworkName}.Schema{metadataSchemaVersion.ToUpper()}");
                settings.AddRegexScrubber(
                    new Regex(@"(http\.url: .*?my-bucket)(?=,)"),
                    "$1/");

                if (!string.IsNullOrWhiteSpace(host))
                {
                    settings.AddSimpleScrubber(host, "localhost:00000");
                }

                settings.DisableRequireUniquePrefix();

                // Note: http.request spans are expected for the S3 APIs that don't have explicit support
                await VerifyHelper.VerifySpans(spans, settings);

                await telemetry.AssertIntegrationEnabledAsync(IntegrationId.AwsS3);
            }
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        public async Task IntegrationDisabled()
        {
            const string expectedOperationName = "aws.s3.request";

            SetEnvironmentVariable($"DD_TRACE_{nameof(IntegrationId.AwsS3)}_ENABLED", "false");

            using var telemetry = this.ConfigureTelemetry();
            string packageVersion = PackageVersions.AwsS3.First()[0] as string;
            using var agent = EnvironmentHelper.GetMockAgent();
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(1, returnAllOperations: true);

                Assert.NotEmpty(spans);
                spans.Where(s => s.Name.Equals(expectedOperationName)).Should().BeEmpty();
                await telemetry.AssertIntegrationDisabledAsync(IntegrationId.AwsS3);
            }
        }
    }
}
