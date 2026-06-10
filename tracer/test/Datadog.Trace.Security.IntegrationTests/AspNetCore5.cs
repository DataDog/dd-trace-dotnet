// <copyright file="AspNetCore5.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
            var spans = await agent.WaitForSpansAsync(1, minDateTime: dateTime);
            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.ScrubSessionFingerprint();
            await VerifyHelper.VerifySpans(spans, settings).UseFileName($"{GetTestName()}.test-null-action");
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestSecurityTestingHeadersTagged()
        {
            // Runs in both SecurityDisabled and SecurityEnabled derivatives — proves the
            // markers land on the entry span unconditionally.
            await TryStartApp();
            var agent = Fixture.Agent;
            var url = "/";
            var dateTime = DateTime.UtcNow;
            var headers = new[]
            {
                new KeyValuePair<string, string>("x-datadog-endpoint-scan", "scan-uuid-1"),
                new KeyValuePair<string, string>("x-datadog-security-test", "test-uuid-2"),
            };
            await SubmitRequest(url, body: null, contentType: null, headers: headers);
            var spans = await agent.WaitForSpansAsync(1, minDateTime: dateTime);
            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.ScrubSessionFingerprint();
            await VerifyHelper.VerifySpans(spans, settings).UseFileName($"{GetTestName()}.security-testing-headers");
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
            await VerifySpans(spans, settings, testName: Prefix + "AspNetCore5.SecurityEnabled.MetaStruct");
        }

        [Trait("RunOnWindows", "True")]
        [SkippableFact]
        public async Task BlockingResponseSecurityIdMatchesAppSecEvent()
        {
            await TryStartApp();

            var agent = Fixture.Agent;

            if (agent.Configuration.SpanMetaStructs)
            {
                await agent.WaitForConfigSentAsync();
            }

            var minDateTime = DateTime.UtcNow;
            var (statusCode, responseText) = await SubmitRequest("/", body: null, contentType: null, userAgent: "Hello/V", accept: "application/json");

            statusCode.Should().Be(HttpStatusCode.Forbidden);

            var responseSecurityId = JObject.Parse(responseText)["security_response_id"]?.Value<string>();
            responseSecurityId.Should().NotBeNullOrEmpty("blocking response should include a security_response_id");

            var spans = await WaitForSpansAsync(agent, expectedSpans: 1, phase: string.Empty, minDateTime, "/");
            var appsecSpan = spans.FirstOrDefault(s => s.MetaStruct.ContainsKey("appsec"));
            appsecSpan.Should().NotBeNull("blocking request should produce an AppSec span");

            var appsecMetaStruct = appsecSpan!.MetaStruct["appsec"];
            var metaStructJson = MetaStructToJson(appsecMetaStruct);
            var spanSecurityId = JToken.Parse(metaStructJson)["triggers"]?.FirstOrDefault()?["security_response_id"]?.Value<string>();

            spanSecurityId.Should().NotBeNullOrEmpty("AppSec event should include a security_response_id");
            spanSecurityId.Should().BeEquivalentTo(responseSecurityId);
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

    public class AspNetCore5TestsResponseStatusBodyCombined : AspNetCoreWithExternalRulesFileBase
    {
        private const string CombinedRuleFile = "ruleset.response-status-body.json";

        public AspNetCore5TestsResponseStatusBodyCombined(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base("AspNetCore5", fixture, outputHelper, "/shutdown", enableSecurity: true, ruleFile: CombinedRuleFile, testName: "AspNetCore5.ResponseStatusBodyCombined")
        {
            Fixture.SetOutput(outputHelper);
        }

        // Regression test for: server.response.status seeded as stale "200" at begin-request would
        // prevent a combined body+status WAF rule from ever firing.
        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestResponseBodyAndStatusCombinedRuleFires()
        {
            await TryStartApp();
            var agent = Fixture.Agent;

            const string url = "/status/404/body";
            var minDateTime = DateTime.UtcNow;

            // The endpoint returns StatusCode(404, { message = "waf_sentinel_response_body" }).
            // The combined rule requires server.response.status == "404" AND server.response.body
            // contains "waf_sentinel_response_body". With the stale-200 bug, this rule could never fire.
            var (statusCode, _) = await SubmitRequest(url, body: null, contentType: null);
            ((int)statusCode).Should().Be((int)HttpStatusCode.Forbidden, "WAF should block the response when body+status combined rule fires");

            var spans = await WaitForSpansAsync(agent, 1, string.Empty, minDateTime, url);
            var appsecSpan = spans.FirstOrDefault(s => s.Tags.ContainsKey("appsec.event"));
            appsecSpan.Should().NotBeNull("a WAF event span must be produced");
            appsecSpan!.Tags.Should().ContainKey("appsec.blocked").WhoseValue.Should().Be("true");

            var appsecJson = appsecSpan.Tags[Tags.AppSecJson];
            appsecJson.Should().Contain("test-response-status-body-001", "the combined body+status rule must have fired");
            appsecJson.Should().Contain("server.response.status", "the status condition must appear in the WAF match");
            appsecJson.Should().Contain("server.response.body", "the body condition must appear in the WAF match");
        }
    }
}
#endif
