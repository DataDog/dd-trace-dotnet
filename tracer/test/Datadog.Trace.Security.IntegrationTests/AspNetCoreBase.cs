// <copyright file="AspNetCoreBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Net;
using System.Threading.Tasks;
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
        [InlineData(true, HttpStatusCode.OK)]
        [InlineData(false, HttpStatusCode.OK)]
        [InlineData(true,  HttpStatusCode.OK, "/Health/?test&[$slice]")]
        [InlineData(true,  HttpStatusCode.NotFound, "/Health/login.php")]
        [Trait("RunOnWindows", "True")]
        public async Task TestSecurity(bool enableSecurity, HttpStatusCode expectedStatusCode, string url = DefaultAttackUrl)
        {
            var agent = await RunOnSelfHosted(enableSecurity);

            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(enableSecurity, (int)expectedStatusCode, sanitisedUrl);

            await TestBlockedRequestWithVerifyAsync(agent, url, null, 5, 1, settings);
        }

        [SkippableTheory]
        [InlineData(true, HttpStatusCode.OK, "/data/model", "property=[$slice]&property2=value2")]
        [InlineData(true, HttpStatusCode.OK, "/dataapi/model", "{\"property\":\"[$slice]\", \"property2\":\"test2\"}")]
        [InlineData(true, HttpStatusCode.OK, "/datarazorpage", "property=[$slice]&property2=value2")]
        [Trait("RunOnWindows", "True")]
        public async Task TestSecurityBody(bool enableSecurity, HttpStatusCode expectedStatusCode, string url, string body)
        {
            var agent = await RunOnSelfHosted(enableSecurity);

            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(enableSecurity, (int)expectedStatusCode, sanitisedUrl, body);
            var contentType = "application/x-www-form-urlencoded";
            if (url.Contains("api"))
            {
                contentType = "application/json";
            }

            await TestBlockedRequestWithVerifyAsync(agent, url, body, 5, 1, settings, contentType);
        }
    }
}
