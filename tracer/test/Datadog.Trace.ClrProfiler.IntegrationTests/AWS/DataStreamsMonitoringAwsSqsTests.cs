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
        foreach (var packageVersionArray in PackageVersions.AwsSqs)
        {
            foreach (var batch in new[] { 0, 1 })
            {
                foreach (var inThread in new[] { 0, 1 })
                {
                    foreach (var inject in new[] { 0, 1 })
                    {
                        yield return [packageVersionArray[0], batch, inThread, inject];
                    }
                }
            }
        }
    }

    [SkippableTheory]
    [MemberData(nameof(GetEnabledConfig))]
    [Trait("Category", "EndToEnd")]
    public async Task SubmitsDsmMetrics(string packageVersion, int batch, int inThread, int inject)
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");

        // set scenario to run
        SetEnvironmentVariable("TEST_BATCH", batch.ToString());
        SetEnvironmentVariable("TEST_IN_THREAD", inThread.ToString());
        SetEnvironmentVariable("TEST_INJECT", inject.ToString());

        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        /*
         * runs a scenario where we test:
         *   in the same thread:
         *    - 1 async send / receive + same with a batch of 3 messages
         *    - 1 async send / receive + same with a batch of 3 messages where headers are full (so we cannot inject an datadog info)
         *   then in 3 different threads:
         *    - 2 async send and 2 async batch send of 3 messages
         *    - 2 async receive of the non batch messages
         *    - 2 async receive of 3 messages of the batch messages
         *   batch messages and single messages are sent to 2 different queues.
         *
         * For DSM, this results in:
         *  - 2 produce pathway points (one for each queue)
         *  - 4 consume pathway points (one with a parent for the "normal" case, and one without for when headers are full, times 2 queues)
         */
        using (RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
        {
#if NETFRAMEWORK
            // there is no snapshot for NetFramework so this test would fail if run
            // but it is compiled, so it still needs to look legit for the CI
            var expectedCount = 0;
#else
            var expectedCount = 9;
#endif
            var spans = agent.WaitForSpans(expectedCount);
            var sqsSpans = spans.Where(
                span => span.Tags.TryGetValue("component", out var component) && component == "aws-sdk");

            sqsSpans.Should().NotBeEmpty();

            var taggedSpans = spans.Where(s => s.Tags.ContainsKey("pathway.hash"));
            taggedSpans.Should().HaveCount(expected: 2); // a send and a receive

            var dsPoints = agent.WaitForDataStreamsPoints(statsCount: 2);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.UseParameters(packageVersion);
            settings.AddDataStreamsScrubber();
            var fileName = $"{nameof(DataStreamsMonitoringAwsSqsTests)}.{nameof(SubmitsDsmMetrics)}."
                         + (batch == 0 ? "single" : "batch") + "."
                         + (inThread == 0 ? "sameThread" : "multiThread") + "."
                         + (inject == 0 ? "noinjection" : "injection");
            await Verifier.Verify(MockDataStreamsPayload.Normalize(dsPoints), settings)
                          .UseFileName(fileName)
                          .DisableRequireUniquePrefix();

            telemetry.AssertIntegrationEnabled(IntegrationId.AwsSqs);
        }
    }
}
