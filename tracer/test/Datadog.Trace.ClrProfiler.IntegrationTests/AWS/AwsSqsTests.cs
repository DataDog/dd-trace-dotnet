// <copyright file="AwsSqsTests.cs" company="Datadog">
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
    public class AwsSqsTests : TestHelper
    {
        public AwsSqsTests(ITestOutputHelper output)
            : base("AWS.SQS", output)
        {
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.AwsSqs), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTraces(string packageVersion)
        {
            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
#if NETFRAMEWORK
                var expectedCount = 56;
                var frameworkName = "NetFramework";
#else
                var expectedCount = 28;
                var frameworkName = "NetCore";
#endif
                var spans = agent.WaitForSpans(expectedCount);

                var host = Environment.GetEnvironmentVariable("AWS_SQS_HOST");

                var settings = VerifyHelper.GetSpanVerifierSettings();
                settings.UseFileName($"{nameof(AwsSqsTests)}.{frameworkName}");
                if (!string.IsNullOrWhiteSpace(host))
                {
                    settings.AddSimpleScrubber(host, "localhost:00000");
                }

                settings.DisableRequireUniquePrefix();

                // Note: http.request spans are expected for the following SQS API's that don't have explicit support
                // - ListQueues
                // - GetQueueUrl
                // - PurgeQueue
                await VerifyHelper.VerifySpans(spans, settings);

                telemetry.AssertIntegrationEnabled(IntegrationId.AwsSqs);
            }
        }
    }
}
