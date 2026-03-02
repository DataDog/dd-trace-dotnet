// <copyright file="AspNetWebApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Immutable;
using System.Linq;
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
    public class AspNetWebApiIntegratedWithSecurity : AspNetWebApi
    {
        public AspNetWebApiIntegratedWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApiIntegratedWithoutSecurity : AspNetWebApi
    {
        public AspNetWebApiIntegratedWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApiClassicWithSecurity : AspNetWebApi
    {
        public AspNetWebApiClassicWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApiClassicWithoutSecurity : AspNetWebApi
    {
        public AspNetWebApiClassicWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: false)
        {
        }
    }

    public abstract class AspNetWebApi : AspNetBase, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private readonly IisFixture _iisFixture;
        private readonly bool _classicMode;

        public AspNetWebApi(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableSecurity)
            : base("WebApi", output, "/api/home/shutdown", @"test\test-applications\security\aspnet")
        {
            SetSecurity(enableSecurity);
            SetEnvironmentVariable(Configuration.ConfigurationKeys.AppSec.Rules, DefaultFullRuleFile);

            _classicMode = classicMode;
            _iisFixture = iisFixture;
            _testName = "Security." + nameof(AspNetWebApi)
                     + (classicMode ? ".Classic" : ".Integrated")
                     + ".enableSecurity=" + enableSecurity; // assume that arm is the same
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [Theory]
        [InlineData("discovery.scans", "/api/Health/wp-config", null)]
        [InlineData(AddressesConstants.RequestQuery, "/api/Health/?arg=[$slice]", null)]
        [InlineData(AddressesConstants.RequestQuery, "/api/Health/?arg&[$slice]", null)]
        [InlineData(AddressesConstants.RequestPathParams, "/api/Health/appscan_fingerprint", null)]
        [InlineData(AddressesConstants.RequestPathParams, "/api/route/2?arg=[$slice]", null)]
        [InlineData(AddressesConstants.RequestPathParams, "/api/route/TwoMember?arg=[$slice]", null)]
        [InlineData(AddressesConstants.RequestBody, "/api/Home/Upload", "{\"Property1\": \"[$slice]\"}")]
        public Task TestSecurity(string test, string url, string body)
        {
            // if blocking is enabled, request stops before reaching asp net mvc integrations intercepting before action methods, so no more spans are generated
            // NOTE: by integrating the latest version of the WAF, blocking was disabled, as it does not support blocking yet.
            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl, body);
            FilterConnectionHeader(settings);
            return TestAppSecRequestWithVerifyAsync(_iisFixture.Agent, url, body, 5, 2, settings, "application/json");
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [SkippableTheory]
        [InlineData("blocking")]
        public async Task TestBlockedRequest(string test)
        {
            var url = "/api/Health";

            var settings = VerifyHelper.GetSpanVerifierSettings(test);
            FilterConnectionHeader(settings);
            // When AppSec is enabled, the request is blocked early and only the ASP.NET root span is created.
            // When AppSec is disabled, the request completes normally and we also get the Web API span.
            var spansPerRequest = SecurityEnabled ? 1 : 2;
            await TestAppSecRequestWithVerifyAsync(_iisFixture.Agent, url, null, 5, spansPerRequest, settings, userAgent: "Hello/V");
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [SkippableTheory]
        [InlineData(AddressesConstants.RequestPathParams, "/api/route/2?arg=[blocking_test]")]
        [InlineData(AddressesConstants.RequestBody, "/api/Home/Upload", "{\"Property1\": \"[blocking_test]\"}")]
        [InlineData(AddressesConstants.ResponseHeaderNoCookies, "/api/asm/injectedheader")] // Blocked on response
        public async Task TestBlockedRequests(string test, string url, string body = null)
        {
            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl, body);
            FilterConnectionHeader(settings);

            var expectedSpans = test == AddressesConstants.RequestPathParams ? 1 : 2;

            if (test == AddressesConstants.ResponseHeaderNoCookies && _classicMode)
            {
                throw new SkipException("Response header injection is not supported in classic mode");
            }

            await TestAppSecRequestWithVerifyAsync(_iisFixture.Agent, url, body, 5, expectedSpans, settings, "application/json");
        }

        [SkippableFact]
        public async Task TestNullAction()
        {
            // test integrations like ReflectedHttpActionDescriptor_ExecuteAsync_Integration and ControllerActionInvoker_InvokeAction_Integration dont crash
            var url = "/api/home/null-action/pathparam/appscan_fingerprint";
            var url2 = "/api/home/null-action-async/pathparam/appscan_fingerprint";
            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.UseTextForParameters($"scenario=null-action");
            FilterConnectionHeader(settings);
            var dateTime = DateTime.UtcNow;
            var res = await SubmitRequest(url, null, null);
            var res2 = await SubmitRequest(url2, null, null);
            var spans = await WaitForSpansAsync(_iisFixture.Agent, 2, null, minDateTime: dateTime, url: url);
            var spans2 = await WaitForSpansAsync(_iisFixture.Agent, 2, null, minDateTime: dateTime, url: url2);
            await VerifySpans(spans.Concat(spans2).ToImmutableList(), settings);
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
