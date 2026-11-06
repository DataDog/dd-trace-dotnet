// <copyright file="AwsDynamoDbTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AWS
{
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "2")]
    [Collection(LocalStackCollection.Name)]
    [UsesVerify]
    public class AwsDynamoDbTests : TracingIntegrationTest
    {
        private readonly LocalStackFixture _localStackFixture;

        public AwsDynamoDbTests(ITestOutputHelper output, LocalStackFixture localStackFixture)
            : base("AWS.DynamoDBv2", output)
        {
            _localStackFixture = localStackFixture;
            ConfigureContainers(localStackFixture);
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.AwsDynamoDb
               select new[] { packageVersionArray[0] };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsAwsDynamoDb();

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTraces(string packageVersion)
        {
            const string metadataSchemaVersion = "v0";
            var clientSpanServiceName = $"{EnvironmentHelper.FullSampleName}-aws-dynamodb";

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
#if NETFRAMEWORK
                var expectedCount = 34;
                var frameworkName = "NetFramework";
#else
                var expectedCount = 17;
                var frameworkName = "NetCore";
#endif
                var spans = await agent.WaitForSpansAsync(expectedCount);
                var dynamoDbSpans = spans.Where(
                    span => span.Tags.TryGetValue("component", out var component) && component == "aws-sdk");

                dynamoDbSpans.Should().NotBeEmpty();
                ValidateIntegrationSpans(dynamoDbSpans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan: true);

                var settings = VerifyHelper.GetSpanVerifierSettings();
                settings.UseFileName($"{nameof(AwsDynamoDbTests)}.{frameworkName}.Schema{metadataSchemaVersion.ToUpper()}");
                settings.AddSimpleScrubber("out.host: localhost", "out.host: aws_dynamodb");
                settings.AddSimpleScrubber($"out.host: {_localStackFixture.Host}", "out.host: aws_dynamodb");
                settings.AddSimpleScrubber("peer.service: localhost", "peer.service: aws_dynamodb");
                settings.AddSimpleScrubber($"peer.service: {_localStackFixture.Host}", "peer.service: aws_dynamodb");
                settings.AddSimpleScrubber(_localStackFixture.HostAndPort, "localhost:00000");
                // V4 uses the sockets handler by default where possible instead of the httpclienthandler
                settings.AddSimpleScrubber("http-client-handler-type: System.Net.Http.SocketsHttpHandler", "http-client-handler-type: System.Net.Http.HttpClientHandler");

                settings.DisableRequireUniquePrefix();

                await VerifyHelper.VerifySpans(spans, settings);

                await telemetry.AssertIntegrationEnabledAsync(IntegrationId.AwsDynamoDb);
            }
        }
    }
}
