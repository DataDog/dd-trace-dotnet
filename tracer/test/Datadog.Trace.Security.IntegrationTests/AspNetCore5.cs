// <copyright file="AspNetCore5.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore5 : AspNetCoreBase, IDisposable
    {
        public AspNetCore5(ITestOutputHelper outputHelper)
            : base("AspNetCore5", outputHelper, "/shutdown")
        {
        }

        [SkippableTheory]
        [InlineData(AddressesConstants.RequestPathParams, true, HttpStatusCode.OK, "/params-endpoint/appscan_fingerprint")]
        [InlineData(AddressesConstants.RequestPathParams, false, HttpStatusCode.OK, "/params-endpoint/appscan_fingerprint")]
        [Trait("RunOnWindows", "True")]
        public async Task TestPathParamsEndpointRouting(string test, bool enableSecurity, HttpStatusCode expectedStatusCode, string url = DefaultAttackUrl)
        {
            var agent = await RunOnSelfHosted(enableSecurity);

            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(test, enableSecurity, (int)expectedStatusCode, sanitisedUrl);
            await TestAppSecRequestWithVerifyAsync(agent, url, null, 5, 1, settings);
        }
    }
}
#endif
