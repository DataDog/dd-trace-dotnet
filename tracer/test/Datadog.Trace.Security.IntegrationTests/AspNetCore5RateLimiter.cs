// <copyright file="AspNetCore5RateLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore5RateLimiter : AspNetBase, IClassFixture<AspNetCoreTestFixture>
    {
        private readonly AspNetCoreTestFixture fixture;

        public AspNetCore5RateLimiter(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base("AspNetCore5", outputHelper, "/shutdown")
        {
            this.fixture = fixture;
            this.fixture.SetOutput(outputHelper);
        }

        public override void Dispose()
        {
            base.Dispose();
            this.fixture.SetOutput(null);
        }

        [SkippableTheory]
        [InlineData(true, 90, 100)]
        [InlineData(false, 90, 100)]
        [InlineData(true, 110, 100)]
        [InlineData(false, 110, 100)]
        [InlineData(true, 30, 20)]
        [InlineData(false, 30, 20)]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "ArmUnsupported")]
        [Trait("SkipInCI", "True")] // Don't run in CI as test is slow, can be run manually by removing this attribute
        public async Task TestRateLimiterSecurity(bool enableSecurity, int totalRequests, int? traceRateLimit, string url = DefaultAttackUrl)
        {
            EnvironmentHelper.CustomEnvironmentVariables.Add("DD_APPSEC_TRACE_RATE_LIMIT", traceRateLimit.ToString());
            await fixture.TryStartApp(this, enableSecurity, externalRulesFile: DefaultRuleFile);
            SetHttpPort(fixture.HttpPort);
            await TestRateLimiter(enableSecurity, url, fixture.Agent, traceRateLimit.GetValueOrDefault(100), totalRequests, 1);
        }
    }
}
#endif
