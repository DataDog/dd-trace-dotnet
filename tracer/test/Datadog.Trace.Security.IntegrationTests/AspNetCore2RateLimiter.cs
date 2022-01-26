// <copyright file="AspNetCore2RateLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP2_1

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore2RateLimiter : AspNetBase, IDisposable
    {
        public AspNetCore2RateLimiter(ITestOutputHelper outputHelper)
            : base("AspNetCore2", outputHelper, "/shutdown")
        {
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "ArmUnsupported")]
        public async Task TestRateLimiterSecurity(bool enableSecurity, string url = DefaultAttackUrl)
        {
            var traceRateLimit = 30;
            var agent = await RunOnSelfHosted(enableSecurity, false, traceRateLimit: traceRateLimit);
            var totalRequests = 120;
            await TestRateLimiter(enableSecurity, url, agent, traceRateLimit, totalRequests, totalRequests);
        }
    }
}
#endif
