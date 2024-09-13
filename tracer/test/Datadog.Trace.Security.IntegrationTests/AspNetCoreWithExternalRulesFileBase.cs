// <copyright file="AspNetCoreWithExternalRulesFileBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyTests;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public abstract class AspNetCoreSecurityDisabledWithExternalRulesFile : AspNetCoreWithExternalRulesFileBase
    {
        public AspNetCoreSecurityDisabledWithExternalRulesFile(string sampleName, AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, string shutdownPath, string ruleFile = null, string testName = null)
            : base(sampleName, fixture, outputHelper, shutdownPath, enableSecurity: false, ruleFile: ruleFile, testName: testName)
        {
        }
    }

    public abstract class AspNetCoreSecurityEnabledWithExternalRulesFileIIS : AspNetCoreWithExternalRulesFileBaseIIS
    {
        public AspNetCoreSecurityEnabledWithExternalRulesFileIIS(string sampleName, IisFixture fixture, ITestOutputHelper outputHelper, string shutdownPath, IisAppType appType, string ruleFile = null, string testName = null)
            : base(sampleName, fixture, outputHelper, shutdownPath, appType, enableSecurity: true, ruleFile: ruleFile, testName: testName, clearMetaStruct: true)
        {
        }

        [SkippableTheory]
        [InlineData(AddressesConstants.ResponseHeaderNoCookies, HttpStatusCode.Forbidden, "/Home/LangHeader")]
        [Trait("RunOnWindows", "True")]
        public async Task TestBlockedHeader(string test, HttpStatusCode expectedStatusCode, string url)
        {
            await TryStartApp();
            var agent = Fixture.Agent;
            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(test, (int)expectedStatusCode, sanitisedUrl);
            await TestAppSecRequestWithVerifyAsync(agent, url, null, 5, 1, settings);
        }
    }

    public abstract class AspNetCoreSecurityEnabledWithExternalRulesFile : AspNetCoreWithExternalRulesFileBase
    {
        public AspNetCoreSecurityEnabledWithExternalRulesFile(string sampleName, AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, string shutdownPath, string ruleFile = null, string testName = null)
            : base(sampleName, fixture, outputHelper, shutdownPath, enableSecurity: true, ruleFile: ruleFile, testName: testName, clearMetaStruct: true)
        {
        }

        [SkippableTheory]
        [InlineData(AddressesConstants.RequestBody, HttpStatusCode.Forbidden, "/data/model", "property=test&property2=dummy_rule")]
        [InlineData(AddressesConstants.RequestBody, HttpStatusCode.Forbidden, "/dataapi/model", "{\"property\":\"dummy_rule\", \"property2\":\"test2\"}")]
        [InlineData(AddressesConstants.RequestBody, HttpStatusCode.Forbidden, "/datarazorpage", "property=dummy_rule&property2=value2")]
        [Trait("RunOnWindows", "True")]
        public async Task TestBlockedBody(string test, HttpStatusCode expectedStatusCode, string url, string body)
        {
            await TryStartApp();
            var agent = Fixture.Agent;

            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(test, (int)expectedStatusCode, sanitisedUrl, body);
            var contentType = "application/x-www-form-urlencoded";
            if (url.Contains("api"))
            {
                contentType = "application/json";
            }

            await TestAppSecRequestWithVerifyAsync(agent, url, body, 5, 1, settings, contentType);
        }

        [SkippableTheory]
        [InlineData(AddressesConstants.ResponseHeaderNoCookies, HttpStatusCode.Forbidden, "/Home/LangHeader")]
        [InlineData(AddressesConstants.ResponseStatus, HttpStatusCode.Forbidden, "/status/418")]
        [InlineData(AddressesConstants.RequestUriRaw, HttpStatusCode.Forbidden, "/health?q=fun")]
        [Trait("RunOnWindows", "True")]
        public async Task TestGet(string test, HttpStatusCode expectedStatusCode, string url)
        {
            VerifierSettings.DisableRequireUniquePrefix();
            await TryStartApp();
            var agent = Fixture.Agent;

            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(test, (int)expectedStatusCode, sanitisedUrl);
            await TestAppSecRequestWithVerifyAsync(agent, url, null, 5, 1, settings);
        }
    }

    public abstract class AspNetCoreWithExternalRulesFileBase : AspNetBase, IClassFixture<AspNetCoreTestFixture>
    {
        public AspNetCoreWithExternalRulesFileBase(
            string sampleName,
            AspNetCoreTestFixture fixture,
            ITestOutputHelper outputHelper,
            string shutdownPath,
            bool enableSecurity = true,
            string ruleFile = null,
            string blockingJsonTemplate = null,
            string blockingHtmlTemplate = null,
            string testName = null,
            bool clearMetaStruct = false)
            : base(
                sampleName,
                outputHelper,
                shutdownPath ?? "/shutdown",
                testName: testName,
                clearMetaStruct: clearMetaStruct)
        {
            EnableSecurity = enableSecurity;
            Fixture = fixture;
            Fixture.SetOutput(outputHelper);
            RuleFile = ruleFile;
            SetEnvironmentVariable(ConfigurationKeys.AppSec.HtmlBlockedTemplate, blockingHtmlTemplate);
            SetEnvironmentVariable(ConfigurationKeys.AppSec.JsonBlockedTemplate, blockingJsonTemplate);
        }

        protected AspNetCoreTestFixture Fixture { get; }

        protected bool EnableSecurity { get; }

        protected string RuleFile { get; }

        public override void Dispose()
        {
            base.Dispose();
            Fixture.SetOutput(null);
        }

        public async Task TryStartApp()
        {
            await Fixture.TryStartApp(this, EnableSecurity, externalRulesFile: RuleFile);
            SetHttpPort(Fixture.HttpPort);
        }

        [SkippableTheory]
        [InlineData("blocking", "/")]
        [Trait("RunOnWindows", "True")]
        public async Task TestBlockedRequest(string test, string url)
        {
            await TryStartApp();
            var agent = Fixture.Agent;

            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl);
            await TestAppSecRequestWithVerifyAsync(agent, url, null, 5, 1, settings, userAgent: "Hello/V");
        }
    }

    public abstract class AspNetCoreWithExternalRulesFileBaseIIS : AspNetBase, IClassFixture<IisFixture>
    {
        public AspNetCoreWithExternalRulesFileBaseIIS(string sampleName, IisFixture fixture, ITestOutputHelper outputHelper, string shutdownPath, IisAppType appType, bool enableSecurity = true, string ruleFile = null, string testName = null, bool clearMetaStruct = false)
            : base(sampleName, outputHelper, shutdownPath ?? "/shutdown", testName: testName, samplesDir: "test\\test-applications\\security", clearMetaStruct: clearMetaStruct)
        {
            EnableSecurity = enableSecurity;
            Fixture = fixture;
            RuleFile = ruleFile;
            AppType = appType;
        }

        protected IisFixture Fixture { get; }

        protected bool EnableSecurity { get; }

        protected string RuleFile { get; }

        protected IisAppType AppType { get; }

        public override void Dispose()
        {
            base.Dispose();
        }

        public async Task TryStartApp()
        {
            SetEnvironmentVariable(ConfigurationKeys.AppSec.Rules, RuleFile);
            SetEnvironmentVariable(ConfigurationKeys.AppSec.Enabled, EnableSecurity.ToString());
            await Fixture.TryStartIis(this, AppType);
            SetHttpPort(Fixture.HttpPort);
        }
    }
}
