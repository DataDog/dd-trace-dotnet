// <copyright file="AspNetMvc5.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.Security.IntegrationTests
{
    [Collection("IisTests")]
    public class AspNetMvc5IntegratedWithSecurity : AspNetMvc5
    {
        public AspNetMvc5IntegratedWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5IntegratedWithoutSecurity : AspNetMvc5
    {
        public AspNetMvc5IntegratedWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: false)
        {
        }
    }

    // in classic mode, you can't access either request or response headers
    // so the verify files for these tests don't contain the attack data
    [Collection("IisTests")]
    public class AspNetMvc5ClassicWithSecurity : AspNetMvc5
    {
        public AspNetMvc5ClassicWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5ClassicWithoutSecurity : AspNetMvc5
    {
        public AspNetMvc5ClassicWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: false)
        {
        }
    }

    public abstract class AspNetMvc5 : AspNetBase, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private readonly IisFixture _iisFixture;
        private readonly bool _classicMode;

        public AspNetMvc5(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableSecurity)
            : base(nameof(AspNetMvc5), output, "/home/shutdown", @"test\test-applications\security\aspnet")
        {
            SetSecurity(enableSecurity);
            SetEnvironmentVariable(Configuration.ConfigurationKeys.AppSec.Rules, DefaultRuleFile);
            SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            SetEnvironmentVariable(Configuration.ConfigurationKeys.AppSec.ApiSecurityEnabled, "false");

            _classicMode = classicMode;
            _iisFixture = iisFixture;
            _testName = "Security." + nameof(AspNetMvc5)
                     + (classicMode ? ".Classic" : ".Integrated")
                     + ".enableSecurity=" + enableSecurity;
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [SkippableTheory]
        [InlineData("discovery.scans", "/Health/wp-config", null)]
        [InlineData(AddressesConstants.RequestQuery, "/Health/?arg=[$slice]", null)]
        [InlineData(AddressesConstants.RequestQuery, "/Health/?arg&[$slice]", null)]
        [InlineData(AddressesConstants.RequestPathParams, "/Health/params/appscan_fingerprint", null)]
        [InlineData(AddressesConstants.RequestPathParams, "/Health/params/appscan_fingerprint?&q=help", null)]
        [InlineData(AddressesConstants.RequestBody, "/Home/Upload", "{\"Property1\": \"[$slice]\"}")]
        [InlineData(AddressesConstants.RequestBody, "/Home/UploadStruct", "{\"Property1\": \"[$slice]\"}")]
        [InlineData(AddressesConstants.RequestBody, "/Home/UploadJson", "{\"DictionaryProperty\": {\"a\":\"[$slice]\"} }")]
        [InlineData(AddressesConstants.ResponseHeaderNoCookies, "/Home/LangHeader", null)]
        public Task TestSecurity(string test, string url, string body)
        {
            // if blocking is enabled, request stops before reaching asp net mvc integrations intercepting before action methods, so no more spans are generated
            // NOTE: by integrating the latest version of the WAF, blocking was disabled, as it does not support blocking yet
            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl, body);
            return TestAppSecRequestWithVerifyAsync(_iisFixture.Agent, url, body, 5, 2, settings, "application/json");
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [SkippableTheory]
        [InlineData("blocking")]
        public async Task TestBlockedRequest(string test)
        {
            var url = "/Health";
            var settings = VerifyHelper.GetSpanVerifierSettings(test);
            await TestAppSecRequestWithVerifyAsync(_iisFixture.Agent, url, null, 5, SecurityEnabled ? 1 : 2, settings, userAgent: "Hello/V");
        }

        public async Task InitializeAsync()
        {
            await _iisFixture.TryStartIis(this, _classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
            SetHttpPort(_iisFixture.HttpPort);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        protected override string GetTestName() => _testName;
    }
}
#endif
