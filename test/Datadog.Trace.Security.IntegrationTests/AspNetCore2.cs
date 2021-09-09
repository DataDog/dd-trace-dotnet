// <copyright file="AspNetCore2.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP2_1
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore2 : AspNetBase, IDisposable
    {
        public AspNetCore2(ITestOutputHelper outputHelper)
            : base("AspNetCore2", outputHelper, "/shutdown")
        {
        }

        // NOTE: by integrating the latest version of the WAF, blocking was disabled, as it does not support blocking yet
        [Theory]
        [InlineData(true, true, HttpStatusCode.OK)]
        [InlineData(true, false, HttpStatusCode.OK)]
        [InlineData(false, true, HttpStatusCode.OK)]
        [InlineData(false, false, HttpStatusCode.OK)]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "ArmUnsupported")]
        public async Task TestSecurity(bool enableSecurity, bool enableBlocking, HttpStatusCode expectedStatusCode)
        {
            using var agent = await RunOnSelfHosted(enableSecurity, enableBlocking);
            await TestBlockedRequestAsync(agent, enableSecurity, expectedStatusCode, 5, new Action<TestHelpers.MockTracerAgent.Span>[]
            {
             s => Assert.Equal("aspnet_core.request", s.Name),
             s  => Assert.Equal("Samples.AspNetCore2", s.Service),
             s  =>  Assert.Equal("web", s.Type)
            });
        }
    }
}
#endif
