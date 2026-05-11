// <copyright file="AspNetMvc5IastTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Security.IntegrationTests.IAST;
using Datadog.Trace.TestHelpers;
using Newtonsoft.Json.Linq;
using VerifyTests;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.Security.IntegrationTests.Iast;

[Collection("IisTests")]
public class AspNetMvc5IntegratedWithIast : AspNetMvc5IastTests
{
    public AspNetMvc5IntegratedWithIast(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: false)
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
        var queryParams = "?contentType=" + contentType + "&returnCode=" + returnCode +
            (string.IsNullOrEmpty(xContentTypeHeaderValue) ? string.Empty : "&xContentTypeHeaderValue=" + xContentTypeHeaderValue);
        var url = "/Iast/XContentTypeHeaderMissing" + queryParams;
        var filename = "Iast.XContentTypeHeaderMissing.AspNetMvc5." + contentType.Replace("/", string.Empty) +
            "." + returnCode.ToString() + "." + (string.IsNullOrEmpty(xContentTypeHeaderValue) ? "empty" : xContentTypeHeaderValue);
        var isHtml = contentType.Contains("html") || contentType.Contains("xhtml");
        var expectVulnerability = isHtml && returnCode == 200 && !xContentTypeHeaderValue.Equals("nosniff", StringComparison.OrdinalIgnoreCase);
        var since = DateTime.UtcNow;
        await SendRequestsAsync(new[] { url });
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, expectVulnerability ? filename : NotVulnerableSnapshotName, since: since, timeoutMs: expectVulnerability ? 5_000 : 1_000);
    }

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
        var queryParams = "?contentType=" + contentType + "&returnCode=" + returnCode +
            (string.IsNullOrEmpty(hstsHeaderValue) ? string.Empty : "&hstsHeaderValue=" + hstsHeaderValue) +
            (string.IsNullOrEmpty(xForwardedProto) ? string.Empty : "&xForwardedProto=" + xForwardedProto);
        var url = "/Iast/StrictTransportSecurity" + queryParams;
        var filename = "Iast.StrictTransportSecurity.AspNetMvc5." + contentType.Replace("/", string.Empty) +
            "." + returnCode.ToString() + "." + (string.IsNullOrEmpty(hstsHeaderValue) ? "empty" : hstsHeaderValue)
            + "." + (string.IsNullOrEmpty(xForwardedProto) ? "empty" : xForwardedProto);
        var isHtml = contentType.Contains("html") || contentType.Contains("xhtml");
        var isHttps = !string.IsNullOrEmpty(xForwardedProto);
        var isValidHsts = Regex.IsMatch(Uri.UnescapeDataString(hstsHeaderValue ?? string.Empty), @"^max-age=[1-9]\d*");
        var expectVulnerability = isHtml && isHttps && returnCode == 200 && !isValidHsts;
        var since = DateTime.UtcNow;
        await SendRequestsAsync(new[] { url });
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, expectVulnerability ? filename : NotVulnerableSnapshotName, since: since, timeoutMs: expectVulnerability ? 5_000 : 1_000);
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
    [InlineData("NotVulnerable", new string[] { "name", "access-control-allow-origin", "origin", "NotVulnerable" }, null, true)]
    [InlineData("Vuln.Cookie.SensitiveValue", new string[] { "name", "set-cookie", "value", "token=glpat-eFynewhuKJFGdfGDFGdw;max-age=31536000;Secure;HttpOnly;SameSite=Strict" }, null)]
    [InlineData("NotVulnerable", null, new string[] { "name", "set-cookie", "value", "NotVulnerable%3D22%3Bmax-age%3D31536000%3BSecure%3BHttpOnly%3BSameSite%3DStrict" })]
    [InlineData("Vuln.MultipleHeaderValues", new string[] { "name", "extraName", "value", "value2" }, null)]
    public async Task TestIastHeaderInjectionRequest(string testCase, string[] headers, string[] cookies, bool useValueFromOriginHeader = false)
    {
        var notVulnerable = testCase.StartsWith("notvulnerable", StringComparison.OrdinalIgnoreCase);
        var filename = "Iast.HeaderInjection.AspNetMvc." + (notVulnerable ? "NotVuln" : testCase) +
            (useValueFromOriginHeader ? ".origin" : string.Empty);
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
        var since = DateTime.UtcNow;
        await SendRequestsAsync(1, new[] { url });
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, notVulnerable ? NotVulnerableSnapshotName : filename, since: since, timeoutMs: notVulnerable ? 1_000 : 5_000);
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData("/Iast/StackTraceLeak")]
    public async Task TestStackTraceLeak(string url)
    {
        var since = DateTime.UtcNow;
        await SendRequestsAsync([url]);
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, GetFileName("StackTraceLeak"), since: since);
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData("/Iast/Print?Encrypt=True&ClientDatabase=774E4D65564946426A53694E48756B592B444A6C43673D3D&p=413&ID=2376&EntityType=114&Print=True&OutputType=WORDOPENXML&SSRSReportID=1")]
    public async Task TestQueryParameterNameVulnerability(string url)
    {
        var since = DateTime.UtcNow;
        await SendRequestsAsync([url]);
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, GetFileName("QueryParameterName"), since: since);
    }
}

[Collection("IisTests")]
public class AspNetMvc5ClassicWithIast : AspNetBase, IClassFixture<IisFixture>, IAsyncLifetime
{
    protected const string NotVulnerableSnapshotName = VulnerabilityJsonl.NotVulnerableSnapshotName;

    private readonly IisFixture _iisFixture;

    public AspNetMvc5ClassicWithIast(IisFixture iisFixture, ITestOutputHelper output)
        : base(nameof(AspNetMvc5), output, "/home/shutdown", @"test\test-applications\security\aspnet")
    {
        EnableEvidenceRedaction(false);
        SetEnvironmentVariable("DD_IAST_DEDUPLICATION_ENABLED", "false");
        SetEnvironmentVariable("DD_IAST_REQUEST_SAMPLING", "100");
        SetEnvironmentVariable("DD_IAST_MAX_CONCURRENT_REQUESTS", "100");
        SetEnvironmentVariable("DD_IAST_VULNERABILITIES_PER_REQUEST", "100");
        SetEnvironmentVariable("DD_APPSEC_STACK_TRACE_ENABLED", "false");
        SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilityLogPath, VulnerabilityLogPath);
        _iisFixture = iisFixture;
        _testName = "Security." + nameof(AspNetMvc5) + ".Classic";
    }

    protected string VulnerabilityLogPath =>
        Path.Combine(LogDirectory, $"iast-vulns-{GetType().Name}.jsonl");

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData("/Iast/QueryOwnUrl")]
    public async Task TestIastFullUrlTaint(string url)
    {
        var sanitisedPath = VerifyHelper.SanitisePathsForVerify(url);
        var filename = $"{_testName}.path={sanitisedPath}";
        var since = DateTime.UtcNow;
        await SendRequestsAsync(new[] { url });
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    public async Task InitializeAsync()
    {
        await _iisFixture.TryStartIis(this, IisAppType.AspNetClassic);
        SetHttpPort(_iisFixture.HttpPort);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected static Task VerifyVulnerabilityRecordsAsync(string path, string fileName, DateTime? since = null, bool includeStack = false, Action<JObject> recordSanitizer = null, int timeoutMs = 5_000)
    {
        return VulnerabilityJsonl.VerifyRecordsAsync(path, fileName, since, includeStack, recordSanitizer, timeoutMs);
    }
}

public abstract class AspNetMvc5IastTests : AspNetBase, IClassFixture<IisFixture>, IAsyncLifetime
{
    protected const string NotVulnerableSnapshotName = VulnerabilityJsonl.NotVulnerableSnapshotName;

    private readonly IisFixture _iisFixture;
    private readonly bool _classicMode;

    public AspNetMvc5IastTests(IisFixture iisFixture, ITestOutputHelper output, bool classicMode)
        : base(nameof(AspNetMvc5), output, "/home/shutdown", @"test\test-applications\security\aspnet")
    {
        EnableEvidenceRedaction(false);
        SetEnvironmentVariable("DD_IAST_DEDUPLICATION_ENABLED", "false");
        SetEnvironmentVariable("DD_IAST_REQUEST_SAMPLING", "100");
        SetEnvironmentVariable("DD_IAST_MAX_CONCURRENT_REQUESTS", "100");
        SetEnvironmentVariable("DD_IAST_VULNERABILITIES_PER_REQUEST", "100");
        SetEnvironmentVariable("DD_APPSEC_STACK_TRACE_ENABLED", "false");
        SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilityLogPath, VulnerabilityLogPath);
        DisableObfuscationQueryString();

        _iisFixture = iisFixture;
        _classicMode = classicMode;
        _testName = "Security." + nameof(AspNetMvc5) + (_classicMode ? ".Classic" : ".Integrated");
    }

    protected string VulnerabilityLogPath =>
        Path.Combine(LogDirectory, $"iast-vulns-{GetType().Name}.jsonl");

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
    [InlineData("/Iast/SafeCookie")]
    [InlineData("/Iast/AllVulnerabilitiesCookie")]
    public async Task TestIastInsecureCookieRequest(string url)
    {
        var expectVulnerability = url.Contains("AllVulnerabilitiesCookie");
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var filename = $"Iast.Vulns.AspNetMvc5.path={sanitisedUrl}";
        var since = DateTime.UtcNow;
        await SendRequestsAsync(new[] { url });
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, expectVulnerability ? filename : NotVulnerableSnapshotName, since: since, timeoutMs: expectVulnerability ? 5_000 : 1_000);
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData("/Iast/SqlQuery?username=Vicent")]
    public async Task TestIastSqlInjectionRequest(string url)
    {
        var since = DateTime.UtcNow;
        await SendRequestsAsync(new[] { url });
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, GetFileName("SqlInjection"), since: since);
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData("/Iast/GetFileContent?file=nonexisting.txt")]
    public async Task TestIastPathTraversalRequest(string url)
    {
        var since = DateTime.UtcNow;
        await SendRequestsAsync(new[] { url });
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, GetFileName("PathTraversal"), since: since);
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData("/Iast/ExecuteCommandFromHeader")]
    public async Task TestIastHeaderTaintingRequest(string url)
    {
        AddHeaders(new Dictionary<string, string>() { { "file", "file.txt" }, { "argumentLine", "arg1" } });
        var since = DateTime.UtcNow;
        await SendRequestsAsync(new[] { url });
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, GetFileName("ExecuteCommandFromHeader"), since: since);
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData("/Iast/ExecuteCommand?file=nonexisting.exe&argumentLine=arg1")]
    public async Task TestIastCommandInjectionRequest(string url)
    {
        var since = DateTime.UtcNow;
        await SendRequestsAsync(new[] { url });
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, GetFileName("CommandInjection"), since: since);
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData("/Iast/SSRF?host=localhost")]
    public async Task TestIastSSRFRequest(string url)
    {
        var since = DateTime.UtcNow;
        await SendRequestsAsync(new[] { url });
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, GetFileName("SSRF"), since: since);
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData("/Iast/WeakRandomness")]
    public async Task TestIastWeakRandomnessRequest(string url)
    {
        var since = DateTime.UtcNow;
        await SendRequestsAsync(new[] { url });
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, GetFileName("WeakRandomness"), since: since);
    }

    [Trait("Category", "LinuxUnsupported")]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData("/Iast/ExecuteQueryFromBodyQueryData", "{\"Query\": \"SELECT Surname from Persons where name='Vicent'\"}")]
    public async Task TestRequestBodyTainting(string url, string body)
    {
        var since = DateTime.UtcNow;
        await SendRequestsAsync(url, body, 1, 1, string.Empty, "application/json", null);
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, GetFileName("RequestBodyTest"), since: since);
    }

    [Trait("Category", "LinuxUnsupported")]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData("/Iast/Ldap?path=LDAP://ldap.forumsys.com:389/dc=example,dc=com")]
    public async Task TestIastLdapRequest(string url)
    {
        var since = DateTime.UtcNow;
        await SendRequestsAsync(new[] { url });
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, GetFileName("Ldap"), since: since);
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData("/Iast/ExecuteCommandFromCookie")]
    public async Task TestIastCookieTaintingRequest(string url)
    {
        AddCookies(new Dictionary<string, string>() { { "file", "file.txt" }, { "argumentLine", "arg1" } });
        var since = DateTime.UtcNow;
        await SendRequestsAsync(new[] { url });
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, GetFileName("CookieTainting"), since: since);
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData("/Iast/TrustBoundaryViolation?name=name&value=value")]
    public async Task TestIastTrustBoundaryViolationRequest(string url)
    {
        var since = DateTime.UtcNow;
        await SendRequestsAsync(new[] { url });
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, GetFileName("TrustBoundaryViolation"), since: since);
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData("/Iast/XpathInjection?user=klaus&value=pass")]
    public async Task TestIastXpathInjectionRequest(string url)
    {
        var since = DateTime.UtcNow;
        await SendRequestsAsync([url]);
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, GetFileName("XpathInjection"), since: since);
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData("/Iast/SendEmail?email=alice@aliceland.com&name=Alice&lastname=Stevens&escape=false")]
    public async Task TestIastEmailHtmlInjectionRequest(string url)
    {
        var since = DateTime.UtcNow;
        await SendRequestsAsync([url]);
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, GetFileName("EmailHtmlInjection"), since: since);
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData("/Iast/UnvalidatedRedirect?param=value")]
    public async Task TestIastUnvalidatedRedirectRequest(string url)
    {
        var since = DateTime.UtcNow;
        await SendRequestsAsync(new[] { url });
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, GetFileName("UnvalidatedRedirect"), since: since);
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableFact]
    public async Task TestIastReflectedXssRequest()
    {
        var url = "/Iast/ReflectedXss?param=<b>RawValue</b>";
        IncludeAllHttpSpans = true;
        var since = DateTime.UtcNow;
        await SendRequestsAsync(2, new[] { url });
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, GetFileName("ReflectedXss"), since: since);
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableFact]
    public async Task TestIastReflectedXssEscapedRequest()
    {
        var url = "/Iast/ReflectedXssEscaped?param=<b>RawValue</b>";
        IncludeAllHttpSpans = true;
        var since = DateTime.UtcNow;
        await SendRequestsAsync(2, new[] { url });
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, NotVulnerableSnapshotName, since: since, timeoutMs: 1_000);
    }

    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData("/Iast/JavaScriptSerializerDeserializeObject?input=nonexisting.exe")]
    public async Task TestJavaScriptSerializerDeserializeObject(string url)
    {
        var since = DateTime.UtcNow;
        await SendRequestsAsync(new[] { url });
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, GetFileName("JavaScriptSerializerDeserializeObject"), since: since);
    }

    protected static Task VerifyVulnerabilityRecordsAsync(string path, string fileName, DateTime? since = null, bool includeStack = false, Action<JObject> recordSanitizer = null, int timeoutMs = 5_000)
    {
        return VulnerabilityJsonl.VerifyRecordsAsync(path, fileName, since, includeStack, recordSanitizer, timeoutMs);
    }

    protected override string GetTestName() => _testName;

    protected string GetFileName(string testName) => $"Iast.{testName}.AspNetMvc5";
}
#endif
