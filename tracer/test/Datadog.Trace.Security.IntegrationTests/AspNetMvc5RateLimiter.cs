// <copyright file="AspNetMvc5RateLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
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
            : base(iisFixture, output, classicMode: false, enableSecurity: true, traceRateLimit: null)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5RateLimiterIntegratedWithoutSecurity : AspNetMvc5RateLimiter
    {
        public AspNetMvc5RateLimiterIntegratedWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: false, traceRateLimit: null)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5RateLimiterClassicWithSecurity : AspNetMvc5RateLimiter
    {
        public AspNetMvc5RateLimiterClassicWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: true, traceRateLimit: null)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5RateLimiterClassicWithoutSecurity : AspNetMvc5RateLimiter
    {
        public AspNetMvc5RateLimiterClassicWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: false, traceRateLimit: null)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5RateLimiterClassicWithSecurityWithCustomTraceRate : AspNetMvc5RateLimiter
    {
        public AspNetMvc5RateLimiterClassicWithSecurityWithCustomTraceRate(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: true, traceRateLimit: 20)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5RateLimiterClassicWithoutSecurityWithCustomTraceRate : AspNetMvc5RateLimiter
    {
        public AspNetMvc5RateLimiterClassicWithoutSecurityWithCustomTraceRate(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: false, traceRateLimit: 20)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5RateLimiterIntegratedWithoutSecurityWithCustomTraceRate : AspNetMvc5RateLimiter
    {
        public AspNetMvc5RateLimiterIntegratedWithoutSecurityWithCustomTraceRate(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: false, traceRateLimit: 20)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5RateLimiterIntegratedWithSecurityWithCustomTraceRate : AspNetMvc5RateLimiter
    {
        public AspNetMvc5RateLimiterIntegratedWithSecurityWithCustomTraceRate(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: true, traceRateLimit: 20)
        {
        }
    }

    public abstract class AspNetMvc5RateLimiter : AspNetBase, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private readonly int? _traceRateLimit = null;
        private readonly IisFixture _iisFixture;
        private readonly bool _enableSecurity;
        private readonly bool _classicMode;

        public AspNetMvc5RateLimiter(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableSecurity, int? traceRateLimit)
            : base(nameof(AspNetMvc5), output, "/home/shutdown", @"test\test-applications\security\aspnet")
        {
            SetSecurity(enableSecurity);
            SetEnvironmentVariable(Configuration.ConfigurationKeys.AppSec.Rules, DefaultRuleFile);
            _classicMode = classicMode;
            _iisFixture = iisFixture;
            _enableSecurity = enableSecurity;
            if (traceRateLimit.HasValue)
            {
                SetEnvironmentVariable(ConfigurationKeys.AppSec.TraceRateLimit, _traceRateLimit.ToString());
            }
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [Trait("SkipInCI", "True")] // Don't run in CI as test is slow, can be run manually by removing this attribute
        [InlineData(110, DefaultAttackUrl)]
        [InlineData(30, DefaultAttackUrl)]
        public async Task TestRateLimiterSecurity(int totalRequests, string url = DefaultAttackUrl)
        {
            // tracing module and mvc actions
            await TestRateLimiter(_enableSecurity, url, _iisFixture.Agent, _traceRateLimit.GetValueOrDefault(100), totalRequests, 2);
            // have to wait a second for the rate limiter to reset (or restart iis express completely)
            Thread.Sleep(1000);
        }

        public async Task InitializeAsync()
        {
            await _iisFixture.TryStartIis(this, _classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
            SetHttpPort(_iisFixture.HttpPort);
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }
}
#endif
