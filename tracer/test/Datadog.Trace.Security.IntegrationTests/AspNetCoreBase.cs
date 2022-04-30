// <copyright file="AspNetCoreBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public abstract class AspNetCoreBase : AspNetBase
    {
        public AspNetCoreBase(string sampleName, ITestOutputHelper outputHelper, string shutdownPath)
            : base(sampleName, outputHelper, shutdownPath ?? "/shutdown")
        {
        }

        [SkippableTheory]
        [InlineData(AddressesConstants.RequestQuery, true, HttpStatusCode.OK, "/Health/?[$slice]=value")]
        [InlineData(AddressesConstants.RequestQuery, false, HttpStatusCode.OK, "/Health/?[$slice]=value")]
        [InlineData(AddressesConstants.RequestQuery, true, HttpStatusCode.OK, "/Health/?arg&[$slice]")]
        [InlineData(AddressesConstants.RequestQuery, false, HttpStatusCode.OK, "/Health/?arg&[$slice]")]

        [InlineData(AddressesConstants.RequestPathParams, true, HttpStatusCode.OK, "/health/params/appscan_fingerprint")]
        [InlineData(AddressesConstants.RequestPathParams, false, HttpStatusCode.OK, "/health/params/appscan_fingerprint")]

        [InlineData("discovery.scans", true, HttpStatusCode.NotFound, "/Health/login.php")]
        [InlineData("discovery.scans", false, HttpStatusCode.OK, "/Health/login.php")]

        [Trait("RunOnWindows", "True")]
        public async Task TestRequest(string test, bool enableSecurity, HttpStatusCode expectedStatusCode, string url = DefaultAttackUrl)
        {
            var agent = await RunOnSelfHosted(enableSecurity);

            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(test, enableSecurity, (int)expectedStatusCode, sanitisedUrl);
            await TestAppSecRequestWithVerifyAsync(agent, url, null, 5, 1, settings);
        }

        [SkippableTheory]
        [InlineData(AddressesConstants.RequestBody, true, HttpStatusCode.OK, "/data/model", "property=[$slice]&property2=value2")]
        [InlineData(AddressesConstants.RequestBody, true, HttpStatusCode.OK, "/dataapi/model", "{\"property\":\"[$slice]\", \"property2\":\"test2\"}")]
        [InlineData(AddressesConstants.RequestBody, true, HttpStatusCode.OK, "/datarazorpage", "property=[$slice]&property2=value2")]
        [Trait("RunOnWindows", "True")]
        public async Task TestBody(string test, bool enableSecurity, HttpStatusCode expectedStatusCode, string url, string body)
        {
            var agent = await RunOnSelfHosted(enableSecurity, externalRulesFile: DefaultRuleFile);

            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(test, enableSecurity, (int)expectedStatusCode, sanitisedUrl, body);
            var contentType = "application/x-www-form-urlencoded";
            if (url.Contains("api"))
            {
                contentType = "application/json";
            }

            await TestAppSecRequestWithVerifyAsync(agent, url, body, 5, 1, settings, contentType);
        }
    }
}
