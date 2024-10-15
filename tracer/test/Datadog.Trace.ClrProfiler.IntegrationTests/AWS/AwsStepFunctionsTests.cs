// <copyright file="AwsStepFunctionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    public class AwsStepFunctionsTests : TracingIntegrationTest
    {
        public AwsStepFunctionsTests(ITestOutputHelper output)
            : base("AWS.StepFunctions", output)
        {
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.AwStepFunctions
               from metadataSchemaVersion in new[] { "v0", "v1" }
               select new[] { packageVersionArray[0], metadataSchemaVersion };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.Tags["span.kind"] switch
        {
            SpanKinds.Client => span.IsAwsStepFunctionsRequest(metadataSchemaVersion),
            _ => throw new ArgumentException($"span.Tags[\"span.kind\"] is not a supported value for the AWS Step Functions integration: {span.Tags["span.kind"]}", nameof(span)),
        };

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTraces(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-aws-stepfunctions" : EnvironmentHelper.FullSampleName;

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
                var stepFunctionsSpans = spans.Where(span => span.Tags.TryGetValue("component", out var component) && component == "aws-sdk");

                stepFunctionsSpans.Should().NotBeEmpty();
                ValidateIntegrationSpans(stepFunctionsSpans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);

                var host = Environment.GetEnvironmentVariable("AWS_SDK_HOST");

                var settings = VerifyHelper.GetSpanVerifierSettings();
                var suffix = GetSnapshotSuffix(packageVersion);

                settings.UseFileName($"{nameof(AwsStepFunctionsTests)}.{frameworkName}.Schema{metadataSchemaVersion.ToUpper()}{suffix}");
                settings.AddSimpleScrubber("out.host: localhost", "out.host: aws_stepfunctions");
                settings.AddSimpleScrubber("out.host: localstack", "out.host: aws_stepfunctions");
                settings.AddSimpleScrubber("out.host: localstack_arm64", "out.host: aws_stepfunctions");
                settings.AddSimpleScrubber("peer.service: localhost", "peer.service: aws_stepfunctions");
                settings.AddSimpleScrubber("peer.service: localstack", "peer.service: aws_stepfunctions");
                settings.AddSimpleScrubber("peer.service: localstack_arm64", "peer.service: aws_stepfunctions");
                if (!string.IsNullOrWhiteSpace(host))
                {
                    settings.AddSimpleScrubber(host, "localhost:00000");
                }

                settings.DisableRequireUniquePrefix();

                await VerifyHelper.VerifySpans(spans, settings);

                telemetry.AssertIntegrationEnabled(IntegrationId.AwsStepFunctions);

                static string GetSnapshotSuffix(string packageVersion)
                    => packageVersion switch
                    {
                        null or "" => ".pre3.7.101.88",
                        { } v when new Version(v) < new Version("3.7.101.88") => ".pre3.7.101.88",
                        _ => string.Empty
                    };
            }
        }
    }
}
