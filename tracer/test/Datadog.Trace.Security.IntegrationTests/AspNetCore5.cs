// <copyright file="AspNetCore5.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// The conditions looks weird, but it seems like _OR_GREATER is not supported yet in all environments
// We can trim all the additional conditions when this is fixed
#if NETCOREAPP3_0 || NETCOREAPP3_1 || NET5_0 || NET6_0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore5 : AspNetBase, IDisposable
    {
        public AspNetCore5(ITestOutputHelper outputHelper)
            : base("AspNetCore5", outputHelper, "/shutdown")
        {
        }

        // NOTE: by integrating the latest version of the WAF, blocking was disabled, as it does not support blocking yet
        [Theory]
        [InlineData(true, true, HttpStatusCode.OK)]
        [InlineData(true, false, HttpStatusCode.OK)]
        [InlineData(false, true, HttpStatusCode.OK)]
        [InlineData(false, false, HttpStatusCode.OK)]
        [InlineData(true, false, HttpStatusCode.OK, "/Health/?test&[$slice]")]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "ArmUnsupported")]
        public async Task TestSecurity(bool enableSecurity, bool enableBlocking, HttpStatusCode expectedStatusCode, string url = DefaultAttackUrl)
        {
            var agent = await RunOnSelfHosted(enableSecurity, enableBlocking);

            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(enableSecurity, enableBlocking, (int)expectedStatusCode, sanitisedUrl);

            await TestBlockedRequestAsync(agent, url, 5, settings);
        }
    }
}
#endif
