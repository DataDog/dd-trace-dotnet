// <copyright file="AspNetCore2.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP2_1

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
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
        [SkippableTheory]
        [InlineData(true, HttpStatusCode.OK)]
        [InlineData(false, HttpStatusCode.OK)]
        [InlineData(true, HttpStatusCode.OK, "/Health/?test&[$slice]")]
        [InlineData(true, HttpStatusCode.NotFound, "/Health/login.php")]
        [Trait("RunOnWindows", "True")]
        public async Task TestSecurity(bool enableSecurity, HttpStatusCode expectedStatusCode, string url = DefaultAttackUrl)
        {
            var agent = await RunOnSelfHosted(enableSecurity);

            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(enableSecurity, (int)expectedStatusCode, sanitisedUrl);

            await TestBlockedRequestWithVerifyAsync(agent, url, 5, 1, settings);
        }
    }
}
#endif
