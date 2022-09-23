// <copyright file="GoGin.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class GoGin : AspNetBase, IDisposable
    {
        public GoGin(ITestOutputHelper outputHelper)
            : base("golang-app", outputHelper, null)
        {
            SetHttpPort(7777);
        }

        [SkippableTheory]
        [InlineData(AddressesConstants.RequestPathParams, true, HttpStatusCode.OK, "/waf/appscan_fingerprint")]
        [InlineData(AddressesConstants.RequestPathParams, false, HttpStatusCode.OK, "/waf/appscan_fingerprint")]
        [Trait("RunOnWindows", "True")]
        public async Task TestPathParamsEndpointRouting(string test, bool enableSecurity, HttpStatusCode expectedStatusCode, string url = DefaultAttackUrl)
        {
            var agent = await RunGoHosted(enableSecurity);

            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(test, enableSecurity, (int)expectedStatusCode, sanitisedUrl);
            await TestAppSecRequestWithVerifyAsync(agent, url, null, 5, 1,  settings);
        }
    }
}
