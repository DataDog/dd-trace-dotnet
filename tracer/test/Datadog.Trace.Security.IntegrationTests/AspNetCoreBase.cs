// <copyright file="AspNetCoreBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public abstract class AspNetCoreBase : AspNetBase, IClassFixture<AspNetCoreTestFixture>
    {
        public AspNetCoreBase(string sampleName, AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, string shutdownPath, bool enableSecurity = true, string testName = null, bool clearMetaStruct = false)
            : base(sampleName, outputHelper, shutdownPath ?? "/shutdown", testName: testName, clearMetaStruct: clearMetaStruct)
        {
            EnableSecurity = enableSecurity;
            Fixture = fixture;
            Fixture.SetOutput(outputHelper);
        }

        protected AspNetCoreTestFixture Fixture { get; }

        protected bool EnableSecurity { get; }

        public override void Dispose()
        {
            base.Dispose();
            Fixture.SetOutput(null);
        }

        public async Task TryStartApp()
        {
            await Fixture.TryStartApp(this, EnableSecurity);
            SetHttpPort(Fixture.HttpPort);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestExternalWafHeaders()
        {
            await TryStartApp();
            var agent = Fixture.Agent;

            var now = DateTime.UtcNow;
            var url = "/?test=external-waf-headers";
            var result = await SubmitRequest(
                             url,
                             null,
                             null,
                             null,
                             accept: null,
                             new List<KeyValuePair<string, string>>
                             {
                                 new("X-SigSci-Tags", "SQLI"),
                                 new("X-Amzn-Trace-Id", "Test"),
                                 new("Cloudfront-Viewer-Ja3-Fingerprint", "Cloudfront-test"),
                                 new("CF-ray", "Test"),
                                 new("X-Cloud-Trace-Context", "Test"),
                                 new("X-Appgw-Trace-id", "Test"),
                                 new("Akamai-User-Risk", "Test"),
                                 new("X-SigSci-RequestID", "Test")
                             });
            var spans = WaitForSpans(agent, 1, string.Empty, now, url);
            var settings = VerifyHelper.GetSpanVerifierSettings();
            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName($"{GetTestName()}.test-external-waf-headers");
        }

        [SkippableTheory]
        [InlineData(AddressesConstants.RequestQuery, HttpStatusCode.OK, "/Health/?[$slice]=value")]
        [InlineData(AddressesConstants.RequestQuery, HttpStatusCode.OK, "/Health/?arg&[$slice]")]
        [InlineData(AddressesConstants.RequestPathParams, HttpStatusCode.OK, "/health/params/appscan_fingerprint")]
        [InlineData(AddressesConstants.RequestPathParams, HttpStatusCode.OK, "/health/params/appscan_fingerprint?&q=help")]
        [InlineData(AddressesConstants.RequestQuery, HttpStatusCode.OK, "/health/params/appscan_fingerprint?[$slice]=value")]
        [Trait("RunOnWindows", "True")]
        public async Task TestRequest(string test, HttpStatusCode expectedStatusCode, string url)
        {
            await TryStartApp();
            var agent = Fixture.Agent;

            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(test, (int)expectedStatusCode, sanitisedUrl);
            await TestAppSecRequestWithVerifyAsync(agent, url, null, 5, 1, settings);
        }

        [SkippableTheory]
        [InlineData("discovery.scans", "/Health/login.php")]
        [Trait("RunOnWindows", "True")]
        public async Task TestDiscoveryScan(string test, string url)
        {
            await TryStartApp();
            var agent = Fixture.Agent;

            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl);
            await TestAppSecRequestWithVerifyAsync(agent, url, null, 5, 1, settings);
        }
    }
}
