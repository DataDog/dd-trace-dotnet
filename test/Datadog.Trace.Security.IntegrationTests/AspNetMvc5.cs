// <copyright file="AspNetMvc5.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET461
using System;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    [Collection("IisTests")]
    public class AspNetMvc5 : AspNetCoreBase
    {
        public AspNetMvc5(ITestOutputHelper outputHelper)
           : base(nameof(AspNetMvc5), outputHelper, "test\\test-applications\\security\\aspnet")
        {
        }

        [Theory]
        [InlineData(true, HttpStatusCode.Forbidden)]
        [InlineData(false, HttpStatusCode.OK)]
        public async Task TestBlockedRequestAsync(bool enableSecurity, HttpStatusCode expectedStatusCode)
        {
            await RunOnIis("/Home", enableSecurity);
            var (statusCode, _) = await SubmitRequest("/Home?arg=[$slice]");
            Assert.Equal(expectedStatusCode, statusCode);
        }
    }
}
#endif
