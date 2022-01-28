// <copyright file="AspNetMvc5RateLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET461
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
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
            : base(iisFixture, output, classicMode: false, enableSecurity: true, blockingEnabled: true, traceRateLimit: null)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5RateLimiterIntegratedWithoutSecurity : AspNetMvc5RateLimiter
    {
        public AspNetMvc5RateLimiterIntegratedWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: false, blockingEnabled: false, traceRateLimit: null)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5RateLimiterClassicWithSecurity : AspNetMvc5RateLimiter
    {
        public AspNetMvc5RateLimiterClassicWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: true, blockingEnabled: false, traceRateLimit: null)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5RateLimiterClassicWithoutSecurity : AspNetMvc5RateLimiter
    {
        public AspNetMvc5RateLimiterClassicWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: false, blockingEnabled: false, traceRateLimit: null)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5RateLimiterClassicWithoutSecurityWithCustomTraceRate : AspNetMvc5RateLimiter
    {
        public AspNetMvc5RateLimiterClassicWithoutSecurityWithCustomTraceRate(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: false, blockingEnabled: false, traceRateLimit: 30)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5RateLimiterClassicWithSecurityWithCustomTraceRate : AspNetMvc5RateLimiter
    {
        public AspNetMvc5RateLimiterClassicWithSecurityWithCustomTraceRate(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: true, blockingEnabled: false, traceRateLimit: 30)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5RateLimiterIntegratedWithoutSecurityWithCustomTraceRate : AspNetMvc5RateLimiter
    {
        public AspNetMvc5RateLimiterIntegratedWithoutSecurityWithCustomTraceRate(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: false, blockingEnabled: false, traceRateLimit: 30)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5RateLimiterIntegratedWithSecurityWithCustomTraceRate : AspNetMvc5RateLimiter
    {
        public AspNetMvc5RateLimiterIntegratedWithSecurityWithCustomTraceRate(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: true, blockingEnabled: false, traceRateLimit: 30)
        {
        }
    }

    public abstract class AspNetMvc5RateLimiter : AspNetBase, IClassFixture<IisFixture>
    {
        private readonly int? _traceRateLimit = null;
        private readonly IisFixture _iisFixture;
        private readonly bool _enableSecurity;
        private readonly bool _blockingEnabled;

        public AspNetMvc5RateLimiter(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableSecurity, bool blockingEnabled, int? traceRateLimit)
            : base(nameof(AspNetMvc5), output, "/home/shutdown", @"test\test-applications\security\aspnet")
        {
            SetSecurity(enableSecurity);
            SetAppSecBlockingEnabled(blockingEnabled);
            _iisFixture = iisFixture;
            _enableSecurity = enableSecurity;
            _blockingEnabled = blockingEnabled;
            if (traceRateLimit.HasValue)
            {
                SetEnvironmentVariable(ConfigurationKeys.AppSecTraceRateLimit, _traceRateLimit.ToString());
            }

            _iisFixture.TryStartIis(this, classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
            SetHttpPort(iisFixture.HttpPort);
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [Theory]
        [InlineData]
        [InlineData(DefaultAttackUrl, 70)]
        public async Task TestRateLimiterSecurity(string url = DefaultAttackUrl, int totalRequests = 50)
        {
            // tracing module and mvc actions
            await TestRateLimiter(_enableSecurity, url, _iisFixture.Agent, _traceRateLimit.GetValueOrDefault(100), totalRequests, totalRequests * 2);
            // have to wait a second for the rate limiter to reset (or restart iis express completely)
            Thread.Sleep(1000);
        }
    }
}
#endif
