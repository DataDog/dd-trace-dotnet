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
    public abstract class AspNetCoreBase : AspNetBase, IClassFixture<AspNetCoreTestFixture>
    {
        public AspNetCoreBase(string sampleName, AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, string shutdownPath)
            : base(sampleName, outputHelper, shutdownPath ?? "/shutdown")
        {
            Fixture = fixture;
            Fixture.SetOutput(outputHelper);
        }

        protected AspNetCoreTestFixture Fixture { get; }

        public override void Dispose()
        {
            base.Dispose();
            Fixture.SetOutput(null);
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
        public async Task TestRequest(string test, bool enableSecurity, HttpStatusCode expectedStatusCode, string url)
        {
            await Fixture.TryStartApp(this, enableSecurity);
            SetHttpPort(Fixture.HttpPort);

            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(test, enableSecurity, (int)expectedStatusCode, sanitisedUrl);
            await TestAppSecRequestWithVerifyAsync(Fixture.Agent, url, null, 5, 1, settings);
        }

        [SkippableTheory]
        [InlineData("blocking", true, HttpStatusCode.Forbidden, "/")]
        [InlineData("blocking", false, HttpStatusCode.OK, "/")]
        [Trait("RunOnWindows", "True")]
        public async Task TestBlockedRequest(string test, bool enableSecurity, HttpStatusCode expectedStatusCode, string url)
        {
            await Fixture.TryStartApp(this, enableSecurity, externalRulesFile: DefaultRuleFile);
            SetHttpPort(Fixture.HttpPort);

            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(test, enableSecurity, (int)expectedStatusCode, sanitisedUrl);
            await TestAppSecRequestWithVerifyAsync(Fixture.Agent, url, null, 5, 1, settings, userAgent: "Hello/V");
        }

        [SkippableTheory]
        [InlineData(AddressesConstants.RequestBody, true, HttpStatusCode.Forbidden, "/data/model", "property=test&property2=dummy_rule")]
        [InlineData(AddressesConstants.RequestBody, true, HttpStatusCode.Forbidden, "/dataapi/model", "{\"property\":\"dummy_rule\", \"property2\":\"test2\"}")]
        [InlineData(AddressesConstants.RequestBody, true, HttpStatusCode.Forbidden, "/datarazorpage", "property=dummy_rule&property2=value2")]
        [Trait("RunOnWindows", "True")]
        public async Task TestBlockedBody(string test, bool enableSecurity, HttpStatusCode expectedStatusCode, string url, string body)
        {
            await Fixture.TryStartApp(this, enableSecurity, externalRulesFile: DefaultRuleFile);
            SetHttpPort(Fixture.HttpPort);

            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(test, enableSecurity, (int)expectedStatusCode, sanitisedUrl, body);
            var contentType = "application/x-www-form-urlencoded";
            if (url.Contains("api"))
            {
                contentType = "application/json";
            }

            await TestAppSecRequestWithVerifyAsync(Fixture.Agent, url, body, 5, 1, settings, contentType);
        }
    }
}
