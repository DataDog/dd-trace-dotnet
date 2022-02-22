// <copyright file="AspNetWebApi.cs" company="Datadog">
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
    public class AspNetWebApiIntegratedWithSecurity : AspNetWebApi
    {
        public AspNetWebApiIntegratedWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: true, blockingEnabled: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApiIntegratedWithSecurityWithoutBlocking : AspNetWebApi
    {
        public AspNetWebApiIntegratedWithSecurityWithoutBlocking(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: true, blockingEnabled: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApiIntegratedWithoutSecurity : AspNetWebApi
    {
        public AspNetWebApiIntegratedWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: false, blockingEnabled: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApiClassicWithSecurity : AspNetWebApi
    {
        public AspNetWebApiClassicWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: true, blockingEnabled: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApiClassicWithSecurityWithoutBlocking : AspNetWebApi
    {
        public AspNetWebApiClassicWithSecurityWithoutBlocking(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: true, blockingEnabled: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApiClassicWithoutSecurity : AspNetWebApi
    {
        public AspNetWebApiClassicWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: false, blockingEnabled: false)
        {
        }
    }

    public abstract class AspNetWebApi : AspNetBase, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;
        private readonly bool _enableSecurity;
        private readonly bool _blockingEnabled;
        private readonly string _testName;

        public AspNetWebApi(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableSecurity, bool blockingEnabled)
            : base("Security.WebApi", output, "/home/shutdown", @"test\test-applications\security\aspnet")
        {
            SetSecurity(enableSecurity);
            _iisFixture = iisFixture;
            _enableSecurity = enableSecurity;
            _blockingEnabled = blockingEnabled;
            _iisFixture.TryStartIis(this, classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
            _testName = "Security." + nameof(AspNetWebApi)
                     + (classicMode ? ".Classic" : ".Integrated")
                     + ".enableSecurity=" + enableSecurity
                     + ".blockingEnabled=" + blockingEnabled; // assume that arm is the same
            SetHttpPort(iisFixture.HttpPort);
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [Theory]
        [InlineData("/api/Health/?test&[$slice]", null)]
        [InlineData("/api/Health/wp-config", null)]
        [InlineData("/api/Health/?arg=[$slice]", null)]
        [InlineData("/api/Home/Upload", "{\"Property1\": \"[$slice]\"}")]
        public Task TestSecurity(string url, string body)
        {
            // if blocking is enabled, request stops before reaching asp net mvc integrations intercepting before action methods, so no more spans are generated
            // NOTE: by integrating the latest version of the WAF, blocking was disabled, as it does not support blocking yet
            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedUrl, body);
            return TestBlockedRequestWithVerifyAsync(_iisFixture.Agent, url, body, 5, 2, "application/json", settings);
        }

        protected override string GetTestName() => _testName;
    }
}
#endif
