// <copyright file="DataStreamsMonitoringAwsSqsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.DataStreamsMonitoring;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AWS;

[Collection(nameof(AwsSqsTests.AwsSqsTestsCollection))]
[Trait("RequiresDockerDependency", "true")]
[UsesVerify]
public class DataStreamsMonitoringAwsSqsTests : TestHelper
{
    public DataStreamsMonitoringAwsSqsTests(ITestOutputHelper output)
        : base("AWS.SQS", output)
    {
    }

    public static IEnumerable<object[]> GetEnabledConfig()
    {
        return from packageVersionArray in PackageVersions.AwsSqs
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
        /*
         * runs a scenario where we test:
         *   in the same thread:
         *    - 2 async send
         *    - 2 async receive
         *    - 2 async batch send of 3 messages each
         *    - 2 async receive of 3 messages
         *   then in 3 different threads:
         *    - 2 async send and 2 async batch send of 3 messages
         *    - 2 async receive of the non batch messages
         *    - 2 async receive of 3 messages of the batch messages
         */
        using (RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
        {
#if NETFRAMEWORK
            // there is no snapshot for NetFramework so this test would fail if run
            // but it is compiled, so it still needs to look legit for the CI
            var expectedCount = 0;
            var frameworkName = "NetFramework";
#else
            var expectedCount = 28;
            var frameworkName = "NetCore";
#endif
            var spans = agent.WaitForSpans(expectedCount);
            var sqsSpans = spans.Where(
                span => span.Tags.TryGetValue("component", out var component) && component == "aws-sdk");

            sqsSpans.Should().NotBeEmpty();

            var taggedSpans = spans.Where(s => s.Tags.ContainsKey("pathway.hash"));
            taggedSpans.Should().HaveCount(expected: 16);

            var dsPoints = agent.WaitForDataStreamsPoints(statsCount: 16);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.UseParameters(packageVersion);
            settings.AddDataStreamsScrubber();
            await Verifier.Verify(MockDataStreamsPayload.Normalize(dsPoints), settings)
                          .UseFileName($"{nameof(DataStreamsMonitoringAwsSqsTests)}.{nameof(SubmitsDsmMetrics)}.{frameworkName}")
                          .DisableRequireUniquePrefix();

            telemetry.AssertIntegrationEnabled(IntegrationId.AwsSqs);
        }
    }
}
