// <copyright file="AspNetCore5IastTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Telemetry;
using Datadog.Trace.Security.IntegrationTests.IAST;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast;

public abstract class AspNetCore5IastTests50PctSamplingIastEnabled : AspNetCore5IastTests
{
    public AspNetCore5IastTests50PctSamplingIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, enableIast: true, testName: "AspNetCore5IastTestsEnabled", isIastDeduplicationEnabled: false, vulnerabilitiesPerRequest: 100, samplingRate: 50)
    {
    }

    public override async Task TryStartApp()
    {
        EnableIast(IastEnabled);
        EnableEvidenceRedaction(RedactionEnabled);
        DisableObfuscationQueryString();
        SetEnvironmentVariable(ConfigurationKeys.Iast.IsIastDeduplicationEnabled, IsIastDeduplicationEnabled?.ToString() ?? string.Empty);
        SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilitiesPerRequest, VulnerabilitiesPerRequest?.ToString() ?? string.Empty);
        SetEnvironmentVariable(ConfigurationKeys.Iast.RequestSampling, SamplingRate?.ToString() ?? string.Empty);
        await Fixture.TryStartApp(this, enableSecurity: false, sendHealthCheck: false);
        SetHttpPort(Fixture.HttpPort);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastWeakHashingRequestSampling()
    {
        var filename = "Iast.WeakHashing.AspNetCore5.IastEnabled";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        await TestWeakHashing(filename, Fixture.Agent);

        filename = "Iast.WeakHashing.AspNetCore5.IastDisabledFlag";
        await TestWeakHashing(filename, Fixture.Agent);

        filename = "Iast.WeakHashing.AspNetCore5.IastEnabled";
        await TestWeakHashing(filename, Fixture.Agent);
    }
}

public class AspNetCore5IastTestsSpanTelemetryIastEnabled : AspNetCore5IastTests
{
    public AspNetCore5IastTestsSpanTelemetryIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
: base(fixture, outputHelper, true, "AspNetCore5IastSpanTelemetryEnabled", iastTelemetryLevel: (int)IastMetricsVerbosityLevel.Debug, samplingRate: 100, isIastDeduplicationEnabled: false, vulnerabilitiesPerRequest: 100)
    {
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastTelemetry()
    {
        var filename = "Iast.PathTraversal.AspNetCore5.TelemetryEnabled";
        var url = "/Iast/GetFileContent?file=nonexisting.txt";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestCookieNameRequest()
    {
        var filename = "Iast.CookieName.AspNetCore5.TelemetryEnabled";
        var url = "/Iast/TestCookieName";
        AddCookies(new Dictionary<string, string>() { { "cookiename", "cookievalue" } });
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }
}

public class AspNetCore5IastTestsOneVulnerabilityPerRequestIastEnabled : AspNetCore5IastTestsVariableVulnerabilityPerRequestIastEnabled
{
    public AspNetCore5IastTestsOneVulnerabilityPerRequestIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
: base(fixture, outputHelper, vulnerabilitiesPerRequest: 1)
    {
    }
}

public class AspNetCore5IastTestsTwoVulnerabilityPerRequestIastEnabled : AspNetCore5IastTestsVariableVulnerabilityPerRequestIastEnabled
{
    public AspNetCore5IastTestsTwoVulnerabilityPerRequestIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
: base(fixture, outputHelper, vulnerabilitiesPerRequest: 2)
    {
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastLocationSpanId()
    {
        var url = "/Iast/WeakHashing";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });
        var parentSpan = spans.First(x => x.ParentId == null);
        var childSpan = spans.First(x => x.ParentId == parentSpan.SpanId);
        var vulnerabilityJson = parentSpan.GetTag(Tags.IastJson);
        vulnerabilityJson.Should().Contain("\"spanId\": " + childSpan.SpanId);
    }
}

public abstract class AspNetCore5IastTestsVariableVulnerabilityPerRequestIastEnabled : AspNetCore5IastTests
{
    public AspNetCore5IastTestsVariableVulnerabilityPerRequestIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, int vulnerabilitiesPerRequest)
        : base(fixture, outputHelper, enableIast: true, testName: "AspNetCore5IastTestsEnabled", isIastDeduplicationEnabled: false, samplingRate: 100, vulnerabilitiesPerRequest: vulnerabilitiesPerRequest)
    {
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastWeakHashingRequestVulnerabilitiesPerRequest()
    {
        var filename = VulnerabilitiesPerRequest == 1 ? "Iast.WeakHashing.AspNetCore5.IastEnabled.SingleVulnerability" : "Iast.WeakHashing.AspNetCore5.IastEnabled";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        await TestWeakHashing(filename, Fixture.Agent);
    }
}

public class AspNetCore5IastTestsFullSamplingIastEnabled : AspNetCore5IastTestsFullSampling
{
    public AspNetCore5IastTestsFullSamplingIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, enableIast: true, vulnerabilitiesPerRequest: 200, isIastDeduplicationEnabled: false, testName: "AspNetCore5IastTestsEnabled")
    {
    }

    // When the request is finished without this X-Content-Type-Options: nosniff header and the content-type of the request looks
    // like html (text/html, application/xhtml+xml) we should detect the vulnerability and send it to the agent.
    // The request is going to be ignored when the response code is one of these: 301, 302, 304, 307, 404, 410, 500.
    // Location: Do not send it
    // Evidence: If the customer application is setting the header with an invalid value, the evidence value should be the value
    // that is set. If the header is missing, the evidence should not be sent.

    [SkippableTheory]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [InlineData("text/html", 200, "nosniff")]
    [InlineData("text/html; charset=UTF-8", 200, "")]
    [InlineData("application/xhtml%2Bxml", 200, "")]
    [InlineData("text/plain", 200, "")]
    [InlineData("text/html", 200, "dummyvalue")]
    [InlineData("text/html", 500, "")]
    public async Task TestIastXContentTypeHeaderMissing(string contentType, int returnCode, string xContentTypeHeaderValue)
    {
        var queryParams = "?contentType=" + contentType + "&returnCode=" + returnCode +
            (string.IsNullOrEmpty(xContentTypeHeaderValue) ? string.Empty : "&xContentTypeHeaderValue=" + xContentTypeHeaderValue);
        var filename = "Iast.XContentTypeHeaderMissing.AspNetCore5." + contentType.Replace("/", string.Empty) +
            "." + returnCode.ToString() + "." + (string.IsNullOrEmpty(xContentTypeHeaderValue) ? "empty" : xContentTypeHeaderValue);
        var url = "/Iast/XContentTypeHeaderMissing" + queryParams;
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing(scrubHash: false);
        await VerifyHelper.VerifySpans(spans, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    // When the request is finished without the header Strict-Transport-Security or with an invalid value on it, we should detect the vulnerability and send it to the agent when these conditions happens:
    // The connection protocol is https or the request header X-Forwarded-Proto is https
    // The Content-Type header of the response looks like html(text/html, application/xhtml+xml)
    // Header has a valid value when it starts with max-age followed by a positive number (>0), it can finish there or continue with a semicolon ; and more content.

    [SkippableTheory]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [InlineData("text/html;charset=UTF-8", 200, "max-age=0", "https")]
    [InlineData("text/html;charset=UTF-8", 200, "max-age=31536000", "https")]
    [InlineData("application/xhtml%2Bxml", 200, "max-age%3D10%3Botherthings", "https")]
    [InlineData("text/html", 500, "invalid", "https")]
    [InlineData("text/html", 200, "invalid", "")]
    [InlineData("text/plain", 200, "invalid", "https")]
    [InlineData("text/html", 200, "", "https")]
    [InlineData("application/xhtml%2Bxml", 200, "", "https")]
    [InlineData("text/html", 200, "invalid", "https")]
    public async Task TestStrictTransportSecurityHeaderMissing(string contentType, int returnCode, string hstsHeaderValue, string xForwardedProto)
    {
        var queryParams = "?contentType=" + contentType + "&returnCode=" + returnCode +
            (string.IsNullOrEmpty(hstsHeaderValue) ? string.Empty : "&hstsHeaderValue=" + hstsHeaderValue) +
            (string.IsNullOrEmpty(xForwardedProto) ? string.Empty : "&xForwardedProto=" + xForwardedProto);
        var filename = "Iast.StrictTransportSecurity.AspNetCore5." + contentType.Replace("/", string.Empty) +
            "." + returnCode.ToString() + "." + (string.IsNullOrEmpty(hstsHeaderValue) ? "empty" : hstsHeaderValue)
            + "." + (string.IsNullOrEmpty(xForwardedProto) ? "empty" : xForwardedProto);
        var url = "/Iast/StrictTransportSecurity" + queryParams;
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing(scrubHash: false);
        await VerifyHelper.VerifySpans(spans, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }
}

    // When the request is finished without this X-Content-Type-Options: nosniff header and the content-type of the request looks
    // like html (text/html, application/xhtml+xml) we should detect the vulnerability and send it to the agent.
    // The request is going to be ignored when the response code is one of these: 301, 302, 304, 307, 404, 410, 500.
    // Location: Do not send it
    // Evidence: If the customer application is setting the header with an invalid value, the evidence value should be the value
    // that is set. If the header is missing, the evidence should not be sent.

    [SkippableTheory]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [InlineData("text/html", 200, "nosniff")]
    [InlineData("text/html", 200, "")]
    [InlineData("application/xhtml%2Bxml", 200, "")]
    [InlineData("text/plain", 200, "")]
    [InlineData("text/html", 200, "dummyvalue")]
    [InlineData("text/html", 500, "")]
    public async Task TestIastXContentTypeHeaderMissing(string contentType, int returnCode, string xContentTypeHeaderValue)
    {
        var commandLine = "?contentType=" + contentType + "&returnCode=" + returnCode +
            (string.IsNullOrEmpty(xContentTypeHeaderValue) ? string.Empty : "&xContentTypeHeaderValue=" + xContentTypeHeaderValue);
        var filename = "Iast.XContentTypeHeaderMissing.AspNetCore5." + contentType.Replace("/", string.Empty) +
            "." + returnCode.ToString() + "." + (string.IsNullOrEmpty(xContentTypeHeaderValue) ? "empty" : xContentTypeHeaderValue);
        var url = "/Iast/XContentTypeHeaderMissing" + commandLine;
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spans, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }
}

public class AspNetCore5IastTestsFullSamplingIastDisabled : AspNetCore5IastTestsFullSampling
{
    public AspNetCore5IastTestsFullSamplingIastDisabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, enableIast: false, testName: "AspNetCore5IastTestsDisabled")
    {
    }
}

public class AspNetCore5IastTestsFullSamplingRedactionEnabled : AspNetCore5IastTestsFullSampling
{
    public AspNetCore5IastTestsFullSamplingRedactionEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, enableIast: true, isIastDeduplicationEnabled: false, testName: "AspNetCore5IastTestsRedactionEnabled", redactionEnabled: true, vulnerabilitiesPerRequest: 100)
    {
    }
}

public abstract class AspNetCore5IastTestsFullSampling : AspNetCore5IastTests
{
    public AspNetCore5IastTestsFullSampling(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableIast, string testName, bool? isIastDeduplicationEnabled = null, int? vulnerabilitiesPerRequest = null, bool redactionEnabled = false)
        : base(fixture, outputHelper, enableIast: enableIast, testName: testName, samplingRate: 100, isIastDeduplicationEnabled: isIastDeduplicationEnabled, vulnerabilitiesPerRequest: vulnerabilitiesPerRequest, redactionEnabled: redactionEnabled)
    {
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastNotWeakRequest()
    {
        var filename = IastEnabled ? "Iast.NotWeak.AspNetCore5.IastEnabled" : "Iast.NotWeak.AspNetCore5.IastDisabled";
        var url = "/Iast";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spans, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastWeakHashingRequest()
    {
        var filename = IastEnabled ? "Iast.WeakHashing.AspNetCore5.IastEnabled" : "Iast.WeakHashing.AspNetCore5.IastDisabled";
        var url = "/Iast/WeakHashing";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spans, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestRequestBodyTaintingRazor()
    {
        var filename = IastEnabled ? "Iast.RequestBodyTestRazor.AspNetCore5.IastEnabled" : "Iast.RequestBodyTestRazor.AspNetCore5.IastDisabled";
        var url = "/DataRazorIastPage";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, url, "property=Execute&property3=2&Property2=nonexisting.exe", 1, 1, string.Empty, "application/x-www-form-urlencoded", null);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
    }

    [SkippableTheory]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [InlineData("{\"Query\": \"SELECT Surname from Persons where name='Vicent'\"}")]
    [InlineData("{\"InnerQuery\": {\"Arguments\": [\"SELECT Surname from Persons where name='Vicent'\"]}}")]
    [InlineData("{\"Arguments\": [\"SELECT Surname from Persons where name='Vicent'\", \"SELECT Surname from Persons where name='Mark'\"]}")]
    [InlineData("{\"StringMap\": {\"query1\": \"SELECT Surname from Persons where name='Vicent'\",\"query2\": \"temp\"}}")]
    [InlineData("{\"StringMap\": {\"\": \"\",\"query2\": \"SELECT Surname from Persons where name='Vicent'\"}}")]
    [InlineData("{\"StringMap\": {\"SELECT Surname from Persons where name='Vicent'\": \"\"}}")]
    [InlineData("{\"StringArrayArguments\": [\"SELECT Surname from Persons where name='Vicent'\", \"SELECT Surname from Persons where name='Mark'\"]}")]
    public async Task TestRequestBodyTainting(string body)
    {
        var filename = IastEnabled ? "Iast.RequestBodyTest.AspNetCore5.IastEnabled" : "Iast.RequestBodyTest.AspNetCore5.IastDisabled";
        var url = "/Iast/ExecuteQueryFromBodyQueryData";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, url, body, 1, 1, string.Empty, "application/json", null);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        var settings = VerifyHelper.GetSpanVerifierSettings();
        var nameRegex = new Regex(@"""name"": ""(\w+)""");
        settings.AddRegexScrubber(nameRegex, string.Empty);
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastSqlInjectionRequest()
    {
        var filename = IastEnabled ? "Iast.SqlInjection.AspNetCore5.IastEnabled" : "Iast.SqlInjection.AspNetCore5.IastDisabled";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/SqlQuery?username=Vicent";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastCommandInjectionRequest()
    {
        var filename = IastEnabled ? "Iast.CommandInjection.AspNetCore5.IastEnabled" : "Iast.CommandInjection.AspNetCore5.IastDisabled";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/ExecuteCommand?file=nonexisting.exe&argumentLine=arg1";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastSSRFRequest()
    {
        var filename = IastEnabled ? "Iast.SSRF.AspNetCore5.IastEnabled" : "Iast.SSRF.AspNetCore5.IastDisabled";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/SSRF?host=localhost";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("Category", "LinuxUnsupported")]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastLdapRequest()
    {
        var filename = IastEnabled ? "Iast.Ldap.AspNetCore5.IastEnabled" : "Iast.Ldap.AspNetCore5.IastDisabled";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/Ldap?userName=Babs Jensen";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastHeaderTaintingRequest()
    {
        var filename = IastEnabled ? "Iast.HeaderTainting.AspNetCore5.IastEnabled" : "Iast.HeaderTainting.AspNetCore5.IastDisabled";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/ExecuteCommandFromHeader";
        IncludeAllHttpSpans = true;
        AddHeaders(new() { { "file", "file.txt" }, { "argumentLine", "arg1" } });
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastCookieTaintingRequest()
    {
        var filename = IastEnabled ? "Iast.CookieTainting.AspNetCore5.IastEnabled" : "Iast.CookieTainting.AspNetCore5.IastDisabled";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/ExecuteCommandFromCookie";
        IncludeAllHttpSpans = true;
        AddCookies(new Dictionary<string, string>() { { "file", "file.txt" }, { "argumentLine", "arg1" } });
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [SkippableTheory]
    [InlineData("/Iast/SafeCookie")]
    [InlineData("/Iast/AllVulnerabilitiesCookie")]
    public async Task TestIastCookiesRequest(string url)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var filename = $"Security.AspNetCore5.enableIast={IastEnabled}.path ={sanitisedUrl}";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing(scrubHash: false);
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastPathTraversalRequest()
    {
        var filename = IastEnabled ? "Iast.PathTraversal.AspNetCore5.IastEnabled" : "Iast.PathTraversal.AspNetCore5.IastDisabled";
        var url = "/Iast/GetFileContent?file=nonexisting.txt";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastWeakRandomnessRequest()
    {
        var filename = IastEnabled ? "Iast.WeakRandomness.AspNetCore5.IastEnabled" : "Iast.WeakRandomness.AspNetCore5.IastDisabled";
        var url = "/Iast/WeakRandomness";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastHardcodedSecretsRequest()
    {
        var filename = "Iast.HardcodedSecrets.AspNetCore5." + (IastEnabled ? "IastEnabled" : "IastDisabled");
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/HardcodedSecrets";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, IastEnabled ? 6 : 2, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web || x.Type == SpanTypes.IastVulnerability).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastTrustBoundaryViolationRequest()
    {
        var filename = "Iast.TrustBoundaryViolation.AspNetCore5." + (IastEnabled ? "IastEnabled" : "IastDisabled");
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/Tbv?name=name&value=value";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, 1, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web || x.Type == SpanTypes.IastVulnerability).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastUnvalidatedRedirectRequest()
    {
        var filename = "Iast.UnvalidatedRedirect.AspNetCore5." + (IastEnabled ? "IastEnabled" : "IastDisabled");
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/UnvalidatedRedirect?param=value";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, 4, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web || x.Type == SpanTypes.IastVulnerability).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
    }
}

public abstract class AspNetCore5IastTests : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
    public AspNetCore5IastTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableIast, string testName, bool? isIastDeduplicationEnabled = null, int? samplingRate = null, int? vulnerabilitiesPerRequest = null, bool? redactionEnabled = false, int iastTelemetryLevel = (int)IastMetricsVerbosityLevel.Off)
        : base("AspNetCore5", outputHelper, "/shutdown", testName: testName)
    {
        Fixture = fixture;
        fixture.SetOutput(outputHelper);
        IastEnabled = enableIast;
        IsIastDeduplicationEnabled = isIastDeduplicationEnabled;
        VulnerabilitiesPerRequest = vulnerabilitiesPerRequest;
        SamplingRate = samplingRate;
        RedactionEnabled = redactionEnabled;
        IastTelemetryLevel = iastTelemetryLevel;
    }

    protected AspNetCoreTestFixture Fixture { get; }

    protected bool IastEnabled { get; }

    protected bool? RedactionEnabled { get; }

    protected bool? IsIastDeduplicationEnabled { get; }

    protected int? VulnerabilitiesPerRequest { get; }

    protected int? SamplingRate { get; }

    protected int IastTelemetryLevel { get; }

    public override void Dispose()
    {
        base.Dispose();
        Fixture.SetOutput(null);
    }

    public virtual async Task TryStartApp()
    {
        EnableIast(IastEnabled);
        EnableEvidenceRedaction(RedactionEnabled);
        EnableIastTelemetry(IastTelemetryLevel);
        SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "1");
        DisableObfuscationQueryString();
        SetEnvironmentVariable(ConfigurationKeys.Iast.IsIastDeduplicationEnabled, IsIastDeduplicationEnabled?.ToString() ?? string.Empty);
        SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilitiesPerRequest, VulnerabilitiesPerRequest?.ToString() ?? string.Empty);
        SetEnvironmentVariable(ConfigurationKeys.Iast.RequestSampling, SamplingRate?.ToString() ?? string.Empty);
        await Fixture.TryStartApp(this, enableSecurity: false);
        SetHttpPort(Fixture.HttpPort);
    }

    protected async Task TestWeakHashing(string filename, MockTracerAgent agent)
    {
        var url = "/Iast/WeakHashing";
        var spans = await SendRequestsAsync(agent, new string[] { url });

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spans, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }
}

#endif
