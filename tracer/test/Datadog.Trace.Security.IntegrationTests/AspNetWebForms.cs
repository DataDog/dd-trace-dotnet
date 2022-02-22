// <copyright file="AspNetWebForms.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET461
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
            : base(iisFixture, output, classicMode: false, enableSecurity: true, blockingEnabled: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebFormsIntegratedWithSecurityWithoutBlocking : AspNetWebForms
    {
        public AspNetWebFormsIntegratedWithSecurityWithoutBlocking(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: true, blockingEnabled: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebFormsIntegratedWithoutSecurity : AspNetWebForms
    {
        public AspNetWebFormsIntegratedWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: false, blockingEnabled: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebFormsClassicWithSecurity : AspNetWebForms
    {
        public AspNetWebFormsClassicWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: true, blockingEnabled: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebFormsClassicWithSecurityWithoutBlocking : AspNetWebForms
    {
        public AspNetWebFormsClassicWithSecurityWithoutBlocking(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: true, blockingEnabled: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebFormsClassicWithoutSecurity : AspNetWebForms
    {
        public AspNetWebFormsClassicWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: false, blockingEnabled: false)
        {
        }
    }

    public abstract class AspNetWebForms : AspNetBase, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;
        private readonly bool _enableSecurity;
        private readonly bool _blockingEnabled;
        private readonly string _testName;

        public AspNetWebForms(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableSecurity, bool blockingEnabled)
            : base("Security.WebForms", output, "/home/shutdown", @"test\test-applications\security\aspnet")
        {
            SetSecurity(enableSecurity);
            _iisFixture = iisFixture;
            _enableSecurity = enableSecurity;
            _blockingEnabled = blockingEnabled;
            _iisFixture.TryStartIis(this, classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
            _testName = "Security." + nameof(AspNetWebForms)
                     + (classicMode ? ".Classic" : ".Integrated")
                     + ".enableSecurity=" + enableSecurity
                     + ".blockingEnabled=" + blockingEnabled; // assume that arm is the same
            SetHttpPort(iisFixture.HttpPort);
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [Theory]
        [InlineData("/Health?test&[$slice]", null)]
        [InlineData("/Health/wp-config", null)]
        [InlineData("/Health?arg=[$slice]", null)]
        [InlineData("/Health", "ctl00%24MainContent%24testBox=%5B%24slice%5D")]
        public Task TestSecurity(string url, string body)
        {
            // if blocking is enabled, request stops before reaching asp net mvc integrations intercepting before action methods, so no more spans are generated
            // NOTE: by integrating the latest version of the WAF, blocking was disabled, as it does not support blocking yet
            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedUrl, body);
            return TestBlockedRequestWithVerifyAsync(_iisFixture.Agent, url, body, 5, 1, settings, "application/x-www-form-urlencoded");
        }

        protected override string GetTestName() => _testName;
    }
}
#endif
