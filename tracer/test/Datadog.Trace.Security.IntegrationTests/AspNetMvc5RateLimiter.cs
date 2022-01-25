// <copyright file="AspNetMvc5RateLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET461
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.Security.IntegrationTests
{
    [Collection("IisTests")]
    public class AspNetMvc5RateLimiterIntegratedWithSecurity : AspNetMvc5RateLimiter
    {
        public AspNetMvc5RateLimiterIntegratedWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: true, blockingEnabled: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5RateLimiterIntegratedWithoutSecurity : AspNetMvc5RateLimiter
    {
        public AspNetMvc5RateLimiterIntegratedWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: false, blockingEnabled: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5RateLimiterClassicWithSecurity : AspNetMvc5RateLimiter
    {
        public AspNetMvc5RateLimiterClassicWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: true, blockingEnabled: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5RateLimiterClassicWithoutSecurity : AspNetMvc5RateLimiter
    {
        public AspNetMvc5RateLimiterClassicWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: false, blockingEnabled: false)
        {
        }
    }

    public abstract class AspNetMvc5RateLimiter : AspNetBase, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;
        private readonly bool _enableSecurity;
        private readonly bool _blockingEnabled;
        private readonly string _testName;

        public AspNetMvc5RateLimiter(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableSecurity, bool blockingEnabled)
            : base(nameof(AspNetMvc5), output, "/home/shutdown", @"test\test-applications\security\aspnet")
        {
            SetSecurity(enableSecurity);
            SetAppSecBlockingEnabled(blockingEnabled);
            _iisFixture = iisFixture;
            _enableSecurity = enableSecurity;
            _blockingEnabled = blockingEnabled;
            _iisFixture.TryStartIis(this, classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
            _testName = "Security." + nameof(AspNetMvc5)
                     + (classicMode ? ".Classic" : ".Integrated")
                     + ".enableSecurity=" + enableSecurity
                     + ".blockingEnabled=" + blockingEnabled; // assume that arm is the same
            SetHttpPort(iisFixture.HttpPort);
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [Theory]
        [InlineData("/Health/?test&[$slice]")]
        [InlineData("/Health/wp-config")]
        [InlineData]
        public async Task TestRateLimiter(string url = DefaultAttackUrl)
        {
            var limit = 30;
            var totalRequests = 120;
            int excess = System.Math.Abs(totalRequests - limit);
            var spans = await SendRequestsAsync(_iisFixture.Agent, url, totalRequests, totalRequests);
            var spansWithUserKeep = spans.Where(s => s.Metrics["_sampling_priority_v1"] == 2.0);
            var spansWithoutUserKeep = spans.Where(s => s.Metrics["_sampling_priority_v1"] != 2.0);
            if (_enableSecurity)
            {
                spansWithUserKeep.Count().Should().BeCloseTo(limit, (uint)(limit * 0.15), "can't be sure it's in the same second");
                var rest = totalRequests - spansWithUserKeep.Count();
                spansWithoutUserKeep.Count().Should().Be(rest);
                spansWithoutUserKeep.Should().Contain(s => s.Metrics.ContainsKey("_dd.appsec.rate_limit.dropped_traces"));
            }
            else
            {
                spansWithoutUserKeep.Count().Should().Be(totalRequests);
                spansWithoutUserKeep.Should().NotContain(s => s.Metrics.ContainsKey("_dd.appsec.rate_limit.dropped_traces"));
            }
        }

        protected override string GetTestName() => _testName;
    }
}
#endif
