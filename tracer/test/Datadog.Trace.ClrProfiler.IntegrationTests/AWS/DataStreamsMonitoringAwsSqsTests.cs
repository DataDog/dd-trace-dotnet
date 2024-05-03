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
                foreach (var inject in new[] { 0, 1 })
                {
                    yield return [packageVersionArray[0], batch, /*sameThread:*/1, inject];
                }

                // there is no multi-thread scenario that don't inject
                yield return [packageVersionArray[0], batch, /*sameThread:*/0, /*inject:*/1];
            }
        }
    }

    [SkippableTheory]
    [MemberData(nameof(GetEnabledConfig))]
    [Trait("Category", "EndToEnd")]
    public async Task SubmitsDsmMetrics(string packageVersion, int batch, int sameThread, int inject)
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");

        // set scenario to run
        SetEnvironmentVariable("TEST_BATCH", batch.ToString());
        SetEnvironmentVariable("TEST_SAME_THREAD", sameThread.ToString());
        SetEnvironmentVariable("TEST_INJECT", inject.ToString());

        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        /*
         * runs a scenario depending on the env variables set, where we do:
         *    - one send
         *    - one receive
         * for batch mode, groups of 3 messages are sent/received
         * "in thread" means that the send and the receive are in separate threads
         * and we can set 10 headers before the instrumentation to prevent it from being able to add one (i.e. prevent injection)
         *
         * For DSM, this results in:
         *  - 1 produce pathway point (tagged with 'direction:out')
         *  - 1 consume pathway points (either with a parent for the "normal" case, or without when headers are full)
         */
        using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
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
                           // whether it's running in the same thread or in different threads shouldn't change anything
                         + (inject == 0 ? "noinjection" : "injection");
            await Verifier.Verify(MockDataStreamsPayload.Normalize(dsPoints), settings)
                          .UseFileName(fileName)
                          .DisableRequireUniquePrefix();

            telemetry.AssertIntegrationEnabled(IntegrationId.AwsSqs);
        }
    }
}
