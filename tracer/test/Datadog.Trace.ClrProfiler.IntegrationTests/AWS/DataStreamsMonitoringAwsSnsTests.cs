// <copyright file="DataStreamsMonitoringAwsSnsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.DataStreamsMonitoring;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AWS;

[Trait("RequiresDockerDependency", "true")]
[Trait("DockerGroup", "2")]
[UsesVerify]
public class DataStreamsMonitoringAwsSnsTests : TestHelper
{
    public DataStreamsMonitoringAwsSnsTests(ITestOutputHelper output)
        : base("AWS.SimpleNotificationService", output)
    {
    }

    public static IEnumerable<object[]> GetEnabledConfig()
    {
        return from packageVersionArray in PackageVersions.AwsSns
               select new[] { packageVersionArray[0] };
    }

    [SkippableTheory]
    [MemberData(nameof(GetEnabledConfig))]
    [Trait("Category", "EndToEnd")]
    public async Task SubmitsDsmMetrics(string packageVersion)
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");

        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
        {
#if NETFRAMEWORK
            var expectedCount = 10;
            var expectedTaggedCount = 4;
            var frameworkName = "NetFramework";
#else
            var expectedCount = 5;
            var expectedTaggedCount = 2;
            var frameworkName = "NetCore";
#endif
            var spans = await agent.WaitForSpansAsync(expectedCount);
            spans.Should().HaveCount(expectedCount);
            var sqsSpans = spans.Where(
                span => span.Tags.TryGetValue("component", out var component) && component == "aws-sdk");

            sqsSpans.Should().NotBeEmpty();

            var taggedSpans = spans.Where(s => s.Tags.ContainsKey("pathway.hash"));
            taggedSpans.Should().HaveCount(expected: expectedTaggedCount);

            var dsPoints = await agent.WaitForDataStreamsPointsAsync(statsCount: expectedTaggedCount);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.UseParameters(packageVersion);
            settings.AddDataStreamsScrubber();
            var fileName = $"{nameof(DataStreamsMonitoringAwsSnsTests)}.{frameworkName}.{nameof(SubmitsDsmMetrics)}";
            await Verifier.Verify(MockDataStreamsPayload.Normalize(dsPoints), settings)
                          .UseFileName(fileName)
                          .DisableRequireUniquePrefix();

            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.AwsSns);
        }
    }
}
