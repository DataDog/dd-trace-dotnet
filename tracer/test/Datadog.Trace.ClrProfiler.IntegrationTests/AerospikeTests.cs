// <copyright file="AerospikeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Trait("RequiresDockerDependency", "true")]
    [UsesVerify]
    public class AerospikeTests : TestHelper
    {
        public AerospikeTests(ITestOutputHelper output)
            : base("Aerospike", output)
        {
            SetServiceVersion("1.0.0");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.Aerospike), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "ArmUnsupported")]
        public async Task SubmitTraces(string packageVersion)
        {
            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                const int expectedSpanCount = 10 + 9; // Sync + async
                var spans = agent.WaitForSpans(expectedSpanCount);

                using var s = new AssertionScope();
                spans.Count.Should().Be(expectedSpanCount);

                var settings = VerifyHelper.GetSpanVerifierSettings();
                await VerifyHelper.VerifySpans(spans, settings)
                                  .DisableRequireUniquePrefix()
                                  .UseFileName(nameof(AerospikeTests));

                telemetry.AssertIntegrationEnabled(IntegrationId.Aerospike);
            }
        }
    }
}
