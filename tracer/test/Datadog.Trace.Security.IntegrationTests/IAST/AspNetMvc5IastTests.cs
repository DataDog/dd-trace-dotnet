// <copyright file="AspNetMvc5IastTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.Iast.Telemetry;
using Datadog.Trace.Security.IntegrationTests.IAST;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.Security.IntegrationTests.Iast;

[Collection("IisTests")]
public class AspNetMvc5IntegratedWithIast : AspNetMvc5IastTests
{
    public AspNetMvc5IntegratedWithIast(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: false, enableIast: true)
    {
    }

    [SkippableTheory]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [InlineData("text/html", 200, "nosniff")]
    [InlineData("text/html;charset=UTF-8", 200, "")]
    [InlineData("application/xhtml%2Bxml", 200, "")]
    [InlineData("text/plain", 200, "")]
    [InlineData("text/html", 200, "dummyvalue")]
    [InlineData("text/html", 500, "")]
    public async Task TestIastXContentTypeHeaderMissing(string contentType, int returnCode, string xContentTypeHeaderValue)
    {
        var testName = "Security." + nameof(AspNetMvc5) + ".Integrated.enableIast=true";
        await TestXContentVulnerability(contentType, returnCode, xContentTypeHeaderValue, testName);
    }

    // When the request is finished without the header Strict-Transport-Security or with aninvalid value on it, we should detect the vulnerability and send it to the agent when these conditions happens:
    // The connection protocol is https or the request header X-Forwarded-Proto is https
    // The Content-Type header of the response looks like html(text/html, application/xhtml+xml)
    // Header has a valid value when it starts with max-age followed by a positive number (>0), it can finish there or continue with a semicolon ; and more content.

    [SkippableTheory]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
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
        var testName = "Security." + nameof(AspNetMvc5) + ".Integrated.IastEnabled";
        await TestStrictTransportSecurityHeaderMissingVulnerability(contentType, returnCode, hstsHeaderValue, xForwardedProto, testName);
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData("Vuln.SensitiveName", new string[] { "name", "private_token" }, new string[] { "value", "ShouldBeRedacted" })]
    [InlineData("Vuln.SensitiveValue", new string[] { "name", "myName", "value", ":bearer secret" }, null)]
    [InlineData("Vuln.SensitiveValueComplex", new string[] { "name", "myName", "value", ":bear" }, new string[] { "value", "er%20secret" })]
    [InlineData("NotVulnerable", new string[] { "propagation", "noVulnValue" }, null)]
    [InlineData("Vuln.NoSensitive", new string[] { "name", "Name", "value", "value" }, new string[] { "value", "moreText" })]
    [InlineData("NotVulnerable", new string[] { "name", "Sec-WebSocket-Accept" }, new string[] { "value", "moreText" })]
    [InlineData("Vuln.Origin", new string[] { "name", "access-control-allow-origin", "value", "https://example.com" }, null)]
    [InlineData("NotVulnerable", new string[] { "name", "access-control-allow-origin", "origin", "NotVulnerable" }, null, true)] // Not vulnerable
    [InlineData("Vuln.Cookie.SensitiveValue", new string[] { "name", "set-cookie", "value", "token=glpat-eFynewhuKJFGdfGDFGdw;max-age=31536000;Secure;HttpOnly;SameSite=Strict" }, null)]
    [InlineData("NotVulnerable", null, new string[] { "name", "set-cookie", "value", "NotVulnerable%3D22%3Bmax-age%3D31536000%3BSecure%3BHttpOnly%3BSameSite%3DStrict" })]
    [InlineData("Vuln.MultipleHeaderValues", new string[] { "name", "extraName", "value", "value2" }, null)]
    public async Task TestIastHeaderInjectionRequest(string testCase, string[] headers, string[] cookies, bool useValueFromOriginHeader = false)
    {
        await TestIastHeaderInjectionRequestVulnerability(testCase, headers, cookies, useValueFromOriginHeader);
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData(AddressesConstants.RequestQuery, "/Iast/StackTraceLeak")]
    public async Task TestStackTraceLeak(string test, string url)
    {
        await TestStrictTransportSecurityHeaderMissingVulnerability(test, url);
    }
}

[Collection("IisTests")]
public class AspNetMvc5IntegratedWithoutIast : AspNetMvc5IastTests
{
    public AspNetMvc5IntegratedWithoutIast(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: false, enableIast: false)
    {
    }

    [SkippableTheory]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [InlineData("text/html", 200, "dummyvalue")]

    public async Task TestIastXContentTypeHeaderMissing(string contentType, int returnCode, string xContentTypeHeaderValue)
    {
        var testName = "Security." + nameof(AspNetMvc5) + ".Classic.enableIast=true";
        await TestXContentVulnerability(contentType, returnCode, xContentTypeHeaderValue, testName);
    }
}

[Collection("IisTests")]
public class AspNetMvc5ClassicWithIast : AspNetMvc5IastTests
{
    public AspNetMvc5ClassicWithIast(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: true, enableIast: true)
    {
    }
}

[Collection("IisTests")]
public class AspNetMvc5ClassicWithIastTelemetryEnabled : AspNetBase, IClassFixture<IisFixture>, IAsyncLifetime
{
    private readonly IisFixture _iisFixture;

    public AspNetMvc5ClassicWithIastTelemetryEnabled(IisFixture iisFixture, ITestOutputHelper output)
        : base(nameof(AspNetMvc5), output, "/home/shutdown", @"test\test-applications\security\aspnet")
    {
        EnableIast(true);
        EnableEvidenceRedaction(false);
        EnableIastTelemetry((int)IastMetricsVerbosityLevel.Debug);
        SetEnvironmentVariable("DD_IAST_DEDUPLICATION_ENABLED", "false");
        SetEnvironmentVariable("DD_IAST_REQUEST_SAMPLING", "100");
        SetEnvironmentVariable("DD_IAST_MAX_CONCURRENT_REQUESTS", "100");
        SetEnvironmentVariable("DD_IAST_VULNERABILITIES_PER_REQUEST", "100");
        SetEnvironmentVariable(Configuration.ConfigurationKeys.AppSec.StackTraceEnabled, "false");

        _iisFixture = iisFixture;
        _testName = "Security." + nameof(AspNetMvc5) + ".TelemetryEnabled" +
                 ".Classic" + ".enableIast=true";
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData(AddressesConstants.RequestQuery, "/Iast/GetFileContent?file=nonexisting.txt")]
    public async Task TestIastTelemetry(string test, string url)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl);
        var spans = await SendRequestsAsync(_iisFixture.Agent, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        settings.AddIastScrubbing();
        var sanitisedPath = VerifyHelper.SanitisePathsForVerify(url);
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName($"{_testName}.path={sanitisedPath}")
                          .DisableRequireUniquePrefix();
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData(AddressesConstants.RequestQuery, "/Iast/QueryOwnUrl")]
    public async Task TestIastFullUrlTaint(string test, string url)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl);
        var spans = await SendRequestsAsync(_iisFixture.Agent, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        settings.AddIastScrubbing();
        var sanitisedPath = VerifyHelper.SanitisePathsForVerify(url);
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName($"{_testName}.path={sanitisedPath}")
                          .DisableRequireUniquePrefix();
    }

    public async Task InitializeAsync()
    {
        await _iisFixture.TryStartIis(this, IisAppType.AspNetClassic);
        SetHttpPort(_iisFixture.HttpPort);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[Collection("IisTests")]
public class AspNetMvc5ClassicWithoutIast : AspNetMvc5IastTests
{
    public AspNetMvc5ClassicWithoutIast(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: true, enableIast: false)
    {
    }
}

public abstract class AspNetMvc5IastTests : AspNetBase, IClassFixture<IisFixture>, IAsyncLifetime
{
    private readonly IisFixture _iisFixture;
    private readonly bool _enableIast;
    private readonly bool _classicMode;

    public AspNetMvc5IastTests(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableIast)
        : base(nameof(AspNetMvc5), output, "/home/shutdown", @"test\test-applications\security\aspnet")
    {
        EnableIast(enableIast);
        EnableIastTelemetry((int)IastMetricsVerbosityLevel.Off);
        EnableEvidenceRedaction(false);
        SetEnvironmentVariable("DD_IAST_DEDUPLICATION_ENABLED", "false");
        SetEnvironmentVariable("DD_IAST_REQUEST_SAMPLING", "100");
        SetEnvironmentVariable("DD_IAST_MAX_CONCURRENT_REQUESTS", "100");
        SetEnvironmentVariable("DD_IAST_VULNERABILITIES_PER_REQUEST", "100");
        DisableObfuscationQueryString();
        SetEnvironmentVariable(Configuration.ConfigurationKeys.AppSec.Rules, DefaultRuleFile);
        SetEnvironmentVariable(Configuration.ConfigurationKeys.AppSec.StackTraceEnabled, "false");

        _iisFixture = iisFixture;
        _classicMode = classicMode;
        _enableIast = enableIast;
        _testName = "Security." + nameof(AspNetMvc5)
                 + (classicMode ? ".Classic" : ".Integrated")
                 + ".enableIast=" + enableIast;
    }

    public async Task InitializeAsync()
    {
        await _iisFixture.TryStartIis(this, _classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
        SetHttpPort(_iisFixture.HttpPort);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData(AddressesConstants.RequestQuery, "/Iast/SafeCookie")]
    [InlineData(AddressesConstants.RequestQuery, "/Iast/AllVulnerabilitiesCookie")]
    public async Task TestIastInsecureCookieRequest(string test, string url)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl);
        var spans = await SendRequestsAsync(_iisFixture.Agent, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        settings.AddIastScrubbing();
        var sanitisedPath = VerifyHelper.SanitisePathsForVerify(url);
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName($"{_testName}.path={sanitisedPath}")
                          .DisableRequireUniquePrefix();
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData(AddressesConstants.RequestQuery, "/Iast/SqlQuery?username=Vicent", null)]
    public async Task TestIastSqlInjectionRequest(string test, string url, string body)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl, body);
        var spans = await SendRequestsAsync(_iisFixture.Agent, new string[] { url });
        var filename = GetFileName("SqlInjection");
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData(AddressesConstants.RequestQuery, "/Iast/GetFileContent?file=nonexisting.txt", null)]
    public async Task TestIastPathTraversalRequest(string test, string url, string body)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl, body);
        var spans = await SendRequestsAsync(_iisFixture.Agent, new string[] { url });
        var filename = GetFileName("PathTraversal");
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData(AddressesConstants.RequestQuery, "/Iast/ExecuteCommandFromHeader", null)]
    public async Task TestIastHeaderTaintingRequest(string test, string url, string body)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl, body);
        AddHeaders(new Dictionary<string, string>() { { "file", "file.txt" }, { "argumentLine", "arg1" } });
        var spans = await SendRequestsAsync(_iisFixture.Agent, new string[] { url });
        var filename = GetFileName("ExecuteCommandFromHeader");
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData(AddressesConstants.RequestQuery, "/Iast/ExecuteCommand?file=nonexisting.exe&argumentLine=arg1", null)]
    public async Task TestIastCommandInjectionRequest(string test, string url, string body)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl, body);
        var spans = await SendRequestsAsync(_iisFixture.Agent, new string[] { url });
        var filename = GetFileName("CommandInjection");
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData(AddressesConstants.RequestQuery, "/Iast/SSRF?host=localhost", null)]
    public async Task TestIastSSRFRequest(string test, string url, string body)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl, body);
        var spans = await SendRequestsAsync(_iisFixture.Agent, new string[] { url });
        var filename = GetFileName("SSRF");
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData(AddressesConstants.RequestQuery, "/Iast/WeakRandomness", null)]
    public async Task TestIastWeakRandomnessRequest(string test, string url, string body)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl, body);
        var spans = await SendRequestsAsync(_iisFixture.Agent, new string[] { url });
        var filename = GetFileName("WeakRandomness");
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [Trait("Category", "LinuxUnsupported")]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData(AddressesConstants.RequestQuery, "/Iast/ExecuteQueryFromBodyQueryData", "{\"Query\": \"SELECT Surname from Persons where name='Vicent'\"}")]
    public async Task TestRequestBodyTainting(string test, string url, string body)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl, body);
        var spans = await SendRequestsAsync(_iisFixture.Agent, url, body, 1, 1, string.Empty, "application/json", null);
        var filename = GetFileName("RequestBodyTest");
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [Trait("Category", "LinuxUnsupported")]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData(AddressesConstants.RequestQuery, "/Iast/Ldap?path=LDAP://fakeorg,DC=com&userName=BabsJensen", null)]
    public async Task TestIastLdapRequest(string test, string url, string body)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl, body);
        var spans = await SendRequestsAsync(_iisFixture.Agent, new string[] { url });
        var filename = GetFileName("Ldap");
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData(AddressesConstants.RequestQuery, "/Iast/ExecuteCommandFromCookie")]
    public async Task TestIastCookieTaintingRequest(string test, string url)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl, null);
        AddCookies(new Dictionary<string, string>() { { "file", "file.txt" }, { "argumentLine", "arg1" } });
        var spans = await SendRequestsAsync(_iisFixture.Agent, new string[] { url });
        var filename = GetFileName("CookieTainting");
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData(AddressesConstants.RequestQuery, "/Iast/TrustBoundaryViolation?name=name&value=value", null)]
    public async Task TestIastTrustBoundaryViolationRequest(string test, string url, string body)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl, body);
        var spans = await SendRequestsAsync(_iisFixture.Agent, new string[] { url });
        var filename = GetFileName("TrustBoundaryViolation");
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData(AddressesConstants.RequestQuery, "/Iast/XpathInjection?user=klaus&value=pass")]
    public async Task TestIastXpathInjectionRequest(string test, string url)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl, null);
        var spans = await SendRequestsAsync(_iisFixture.Agent, [url]);
        var filename = GetFileName("XpathInjection");
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData(AddressesConstants.RequestQuery, "/Iast/SendEmail?email=alice@aliceland.com&name=Alice&lastname=Stevens&escape=false")]
    public async Task TestIastEmailHtmlInjectionRequest(string test, string url)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl, null);
        var spans = await SendRequestsAsync(_iisFixture.Agent, [url]);
        var filename = GetFileName("EmailHtmlInjection");
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData(AddressesConstants.RequestQuery, "/Iast/UnvalidatedRedirect?param=value", null)]
    public async Task TestIastUnvalidatedRedirectRequest(string test, string url, string body)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl, body);
        var spans = await SendRequestsAsync(_iisFixture.Agent, new string[] { url });
        var filename = GetFileName("UnvalidatedRedirect");
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastReflectedXssRequest()
    {
        (Regex RegexPattern, string Replacement) pathScrubber = (new Regex("\"path\": \"AspNetCore[^\\.]+\\."), "\"path\": \"AspNetCore.");
        (Regex RegexPattern, string Replacement) hashScrubber = (new Regex("\"hash\": -953468592,"), "\"hash\": -623616875,");

        var filename = GetFileName("ReflectedXss");
        var url = "/Iast/ReflectedXss?param=<b>RawValue</b>";
        IncludeAllHttpSpans = true;
        var spans = await SendRequestsAsync(_iisFixture.Agent, 2, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web || x.Type == SpanTypes.IastVulnerability).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        settings.AddRegexScrubber(pathScrubber);
        settings.AddRegexScrubber(hashScrubber);
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastReflectedXssEscapedRequest()
    {
        var filename = GetFileName("ReflectedXssEscaped");
        var url = "/Iast/ReflectedXssEscaped?param=<b>RawValue</b>";
        IncludeAllHttpSpans = true;
        var spans = await SendRequestsAsync(_iisFixture.Agent, 2, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web || x.Type == SpanTypes.IastVulnerability).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
    }

    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData(AddressesConstants.RequestQuery, "/Iast/JavaScriptSerializerDeserializeObject?input=nonexisting.exe", null)]
    public async Task TestJavaScriptSerializerDeserializeObject(string test, string url, string body)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl, body);
        var spans = await SendRequestsAsync(_iisFixture.Agent, new string[] { url });
        var filename = GetFileName("JavaScriptSerializerDeserializeObject");
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    protected async Task TestStrictTransportSecurityHeaderMissingVulnerability(string test, string url)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl);
        var spans = await SendRequestsAsync(_iisFixture.Agent, [url]);
        var filename = GetFileName("StackTraceLeak");
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    protected async Task TestStrictTransportSecurityHeaderMissingVulnerability(string contentType, int returnCode, string hstsHeaderValue, string xForwardedProto, string testName)
    {
        var queryParams = "?contentType=" + contentType + "&returnCode=" + returnCode +
                    (string.IsNullOrEmpty(hstsHeaderValue) ? string.Empty : "&hstsHeaderValue=" + hstsHeaderValue) +
                    (string.IsNullOrEmpty(xForwardedProto) ? string.Empty : "&xForwardedProto=" + xForwardedProto);
        var url = "/Iast/StrictTransportSecurity" + queryParams;
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(AddressesConstants.RequestQuery, sanitisedUrl);
        var spans = await SendRequestsAsync(_iisFixture.Agent, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        settings.AddIastScrubbing();
        var filename = testName + "." + contentType.Replace("/", string.Empty) +
            "." + returnCode.ToString() + "." + (string.IsNullOrEmpty(hstsHeaderValue) ? "empty" : hstsHeaderValue)
            + "." + (string.IsNullOrEmpty(xForwardedProto) ? "empty" : xForwardedProto);
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    protected async Task TestIastHeaderInjectionRequestVulnerability(string testCase, string[] headers, string[] cookies, bool useValueFromOriginHeader = false)
    {
        var notVulnerable = testCase.StartsWith("notvulnerable", StringComparison.OrdinalIgnoreCase) || !_enableIast;
        var filename = "Iast.HeaderInjection.AspNetMvc." + (notVulnerable ? "NotVuln" : testCase) +
            (useValueFromOriginHeader ? ".origin" : string.Empty);
        if (!_enableIast) { filename += ".IastDisabled"; }
        var url = $"/Iast/HeaderInjection?useValueFromOriginHeader={useValueFromOriginHeader}";
        IncludeAllHttpSpans = true;

        Dictionary<string, string> headersDic = new();
        Dictionary<string, string> cookiesDic = new();

        if (headers != null)
        {
            for (int i = 0; i < headers.Length; i = i + 2)
            {
                headersDic.Add(headers[i], headers[i + 1]);
            }
        }

        if (cookies != null)
        {
            for (int i = 0; i < cookies.Length; i = i + 2)
            {
                cookiesDic.Add(cookies[i], cookies[i + 1]);
            }
        }

        AddCookies(cookiesDic);
        AddHeaders(headersDic);

        var spans = await SendRequestsAsync(_iisFixture.Agent, 1, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web || x.Type == SpanTypes.IastVulnerability).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
    }

    protected async Task TestXContentVulnerability(string contentType, int returnCode, string xContentTypeHeaderValue, string testName)
    {
        var queryParams = "?contentType=" + contentType + "&returnCode=" + returnCode +
            (string.IsNullOrEmpty(xContentTypeHeaderValue) ? string.Empty : "&xContentTypeHeaderValue=" + xContentTypeHeaderValue);
        var url = "/Iast/XContentTypeHeaderMissing" + queryParams;
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(AddressesConstants.RequestQuery, sanitisedUrl);
        var spans = await SendRequestsAsync(_iisFixture.Agent, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName($"{testName}.path={sanitisedUrl}")
                          .DisableRequireUniquePrefix();
    }

    protected override string GetTestName() => _testName;

    private string GetFileName(string testName)
    {
        return $"Iast.{testName}.AspNetMvc5" + (_enableIast ? ".IastEnabled" : ".IastDisabled");
    }
}
#endif
