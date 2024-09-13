// <copyright file="AspNetWebForms.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.Net;
using System.Security.Policy;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.Security.IntegrationTests
{
    [Collection("IisTests")]
    public class AspNetWebFormsIntegratedWithSecurity : AspNetWebForms
    {
        public AspNetWebFormsIntegratedWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebFormsIntegratedWithoutSecurity : AspNetWebForms
    {
        public AspNetWebFormsIntegratedWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebFormsClassicWithSecurity : AspNetWebForms
    {
        public AspNetWebFormsClassicWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebFormsClassicWithoutSecurity : AspNetWebForms
    {
        public AspNetWebFormsClassicWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: false)
        {
        }
    }

    public abstract class AspNetWebForms : AspNetBase, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private readonly IisFixture _iisFixture;
        private readonly bool _classicMode;

        public AspNetWebForms(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableSecurity)
            : base("WebForms", output, "/home/shutdown", @"test\test-applications\security\aspnet")
        {
            SetSecurity(enableSecurity);
            SetEnvironmentVariable(Configuration.ConfigurationKeys.AppSec.Rules, DefaultRuleFile);
            SetEnvironmentVariable(Configuration.ConfigurationKeys.AppSec.StackTraceEnabled, "false");

            _iisFixture = iisFixture;
            _classicMode = classicMode;
            _testName = "Security." + nameof(AspNetWebForms)
                     + (classicMode ? ".Classic" : ".Integrated")
                     + ".enableSecurity=" + enableSecurity;
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [Theory]
        [InlineData("/Health?test&[$slice]", null)]
        [InlineData("/Health/Params/appscan_fingerprint", null)]
        [InlineData("/Health/wp-config", null)]
        [InlineData("/Health?arg=[$slice]", null)]
        [InlineData("/Health", "ctl00%24MainContent%24testBox=%5B%24slice%5D")]
        public Task TestSecurity(string url, string body)
        {
            // if blocking is enabled, request stops before reaching asp net mvc integrations intercepting before action methods, so no more spans are generated
            // NOTE: by integrating the latest version of the WAF, blocking was disabled, as it does not support blocking yet
            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedUrl, body);
            return TestAppSecRequestWithVerifyAsync(_iisFixture.Agent, url, body, 5, 1, settings, "application/x-www-form-urlencoded");
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
            await TestAppSecRequestWithVerifyAsync(_iisFixture.Agent, url, null, 5, 1, settings, userAgent: "Hello/V");
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
