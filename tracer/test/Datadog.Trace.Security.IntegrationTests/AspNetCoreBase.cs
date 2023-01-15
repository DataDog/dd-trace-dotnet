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
        public AspNetCoreBase(string sampleName, AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, string shutdownPath, bool enableSecurity = true, string testName = null)
            : base(sampleName, outputHelper, shutdownPath ?? "/shutdown", testName: testName)
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

        [SkippableTheory]
        [InlineData(AddressesConstants.RequestQuery, HttpStatusCode.OK, "/Health/?[$slice]=value")]
        [InlineData(AddressesConstants.RequestQuery, HttpStatusCode.OK, "/Health/?arg&[$slice]")]
        [InlineData(AddressesConstants.RequestPathParams, HttpStatusCode.OK, "/health/params/appscan_fingerprint")]
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
