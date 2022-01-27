// <copyright file="AspNetCore5RateLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore5RateLimiter : AspNetBase, IDisposable
    {
        public AspNetCore5RateLimiter(ITestOutputHelper outputHelper)
            : base("AspNetCore5", outputHelper, "/shutdown")
        {
        }

        [Theory]
        [InlineData(true, 140)]
        [InlineData(false, 140)]
        [InlineData(true, 90)]
        [InlineData(false, 90)]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "ArmUnsupported")]
        public async Task TestRateLimiterSecurity(bool enableSecurity, int totalRequests, string url = DefaultAttackUrl)
        {
            var traceRateLimit = 90;
            var agent = await RunOnSelfHosted(enableSecurity, false, traceRateLimit: traceRateLimit);
            await TestRateLimiter(enableSecurity, url, agent, traceRateLimit, totalRequests, totalRequests);
        }
    }
}
#endif
