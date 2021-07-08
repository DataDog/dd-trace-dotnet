// <copyright file="AspNetMvc5.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET461
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    [CollectionDefinition("IisTests", DisableParallelization = true)]
    [Collection("IisTests")]
    public class AspNetMvc5 : AspNetBase
    {
        public AspNetMvc5(ITestOutputHelper outputHelper)
           : base(nameof(AspNetMvc5), outputHelper, "test\\test-applications\\security\\aspnet")
        {
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [InlineData(true, HttpStatusCode.Forbidden)]
        [InlineData(false, HttpStatusCode.OK)]
        public async Task TestSecurity(bool enableSecurity, HttpStatusCode expectedStatusCode)
        {
            var agent = await RunOnIis("/Home", enableSecurity);
            await TestBlockedRequestAsync(agent, enableSecurity, expectedStatusCode, enableSecurity ? 5 : 10, new Action<TestHelpers.MockTracerAgent.Span>[]
            {
             s => Assert.Matches("aspnet(-mvc)?.request", s.Name),
             s => Assert.Equal("Development Web Site", s.Service),
             s => Assert.Equal("web", s.Type)
            });
        }
    }
}
#endif
