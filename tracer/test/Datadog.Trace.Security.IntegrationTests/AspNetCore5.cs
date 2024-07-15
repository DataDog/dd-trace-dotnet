// <copyright file="AspNetCore5.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore5TestsSecurityDisabled : AspNetCore5TestsWithoutExternalRulesFile
    {
        public AspNetCore5TestsSecurityDisabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: false, testName: "AspNetCore5.SecurityDisabled")
        {
        }
    }

    public class AspNetCore5TestsSecurityEnabled : AspNetCore5TestsWithoutExternalRulesFile
    {
        public AspNetCore5TestsSecurityEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: true, testName: "AspNetCore5.SecurityEnabled")
        {
        }
    }

    public abstract class AspNetCore5TestsWithoutExternalRulesFile : AspNetCoreBase
    {
        public AspNetCore5TestsWithoutExternalRulesFile(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableSecurity, string testName)
            : base("AspNetCore5", fixture, outputHelper, "/shutdown", enableSecurity: enableSecurity, testName: testName)
        {
            SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "false");
        }

        [SkippableTheory]
        [InlineData(AddressesConstants.RequestPathParams, HttpStatusCode.OK, "/params-endpoint/appscan_fingerprint")]
        [Trait("RunOnWindows", "True")]
        public async Task TestPathParamsEndpointRouting(string test, HttpStatusCode expectedStatusCode, string url)
        {
            await TryStartApp();
            var agent = Fixture.Agent;

            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(test, (int)expectedStatusCode, sanitisedUrl);

            // for .NET 7+, the endpoint names changed from
            // aspnet_core.endpoint: /params-endpoint/{s} HTTP: GET,
            // to
            // aspnet_core.endpoint: HTTP: GET /params-endpoint/{s},
            // So normalize to the .NET 6 pattern for simplicity
#if NET7_0_OR_GREATER
            settings.AddSimpleScrubber("HTTP: GET /params-endpoint/{s}", "/params-endpoint/{s} HTTP: GET");
#endif
            await TestAppSecRequestWithVerifyAsync(agent, url, null, 5, 1, settings);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestNullAction()
        {
            await TryStartApp();
            var agent = Fixture.Agent;
            var url = "/null-action/test/test";
            var dateTime = DateTime.UtcNow;
            await SubmitRequest(url, null, null);
            var spans = agent.WaitForSpans(1, minDateTime: dateTime);
            var settings = VerifyHelper.GetSpanVerifierSettings();
            await VerifyHelper.VerifySpans(spans, settings).UseFileName($"{GetTestName()}.test-null-action");
        }
    }

    public class AspNetCore5TestsSecurityDisabledWithDefaultExternalRulesFile : AspNetCoreSecurityDisabledWithExternalRulesFile
    {
        public AspNetCore5TestsSecurityDisabledWithDefaultExternalRulesFile(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base("AspNetCore5", fixture, outputHelper, "/shutdown", ruleFile: DefaultRuleFile, testName: "AspNetCore5.SecurityDisabled")
        {
        }
    }

    public class AspNetCore5TestsSecurityEnabledWithDefaultExternalRulesFile : AspNetCoreSecurityEnabledWithExternalRulesFile
    {
        public AspNetCore5TestsSecurityEnabledWithDefaultExternalRulesFile(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base("AspNetCore5", fixture, outputHelper, "/shutdown", ruleFile: DefaultRuleFile, testName: "AspNetCore5.SecurityEnabled")
        {
        }

        [Trait("RunOnWindows", "True")]
        [SkippableFact]
        public async Task TestAppsecMetaStruct()
        {
            await TryStartApp();
            var agent = Fixture.Agent;
            var url = "/health?q=fun";

            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedUrl);
            var spans = await SendRequestsAsync(agent, url, null, 1, 1, string.Empty);
            await VerifySpans(spans, settings, testName: "AspNetCore5.SecurityEnabled.MetaStruct", forceMetaStruct: true);
        }
    }

    [Collection("IisTests")]
    [Trait("Category", "LinuxUnsupported")]
    public class AspNetCore5TestsSecurityEnabledWithDefaultExternalRulesFileIIS : AspNetCoreSecurityEnabledWithExternalRulesFileIIS
    {
        public AspNetCore5TestsSecurityEnabledWithDefaultExternalRulesFileIIS(IisFixture fixture, ITestOutputHelper outputHelper)
            : base("AspNetCore5", fixture, outputHelper, "/shutdown", IisAppType.AspNetCoreInProcess, ruleFile: AppDomain.CurrentDomain.BaseDirectory + DefaultRuleFile, testName: "AspNetCore5.SecurityEnabledIIS")
        {
        }
    }
}
#endif
