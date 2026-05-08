// <copyright file="AspNetCore2IastTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP2_1
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

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

namespace Datadog.Trace.Security.IntegrationTests.Iast;

public class AspNetCore2IastTestsOneVulnerabilityPerRequestIastEnabled : AspNetCore2IastTestsVariableVulnerabilityPerRequestIastEnabled
{
    public AspNetCore2IastTestsOneVulnerabilityPerRequestIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
: base(fixture, outputHelper, vulnerabilitiesPerRequest: 1)
    {
    }
}

public class AspNetCore2IastTestsTwoVulnerabilityPerRequestIastEnabled : AspNetCore2IastTestsVariableVulnerabilityPerRequestIastEnabled
{
    public AspNetCore2IastTestsTwoVulnerabilityPerRequestIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
: base(fixture, outputHelper, vulnerabilitiesPerRequest: 2)
    {
    }
}

public abstract class AspNetCore2IastTestsVariableVulnerabilityPerRequestIastEnabled : AspNetCore2IastTests
{
    public AspNetCore2IastTestsVariableVulnerabilityPerRequestIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, int vulnerabilitiesPerRequest)
        : base(fixture, outputHelper, testName: "AspNetCore2IastTestsEnabled", isIastDeduplicationEnabled: false, samplingRate: 100, vulnerabilitiesPerRequest: vulnerabilitiesPerRequest)
    {
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastWeakHashingRequestVulnerabilitiesPerRequest()
    {
        IncludeAllHttpSpans = true;
        var filename = VulnerabilitiesPerRequest == 1 ? "Iast.WeakHashing.Vulns.AspNetCore2.SingleVulnerability" : "Iast.WeakHashing.Vulns.AspNetCore2";
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, ["/Iast/WeakHashing"]);

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }
}

public class AspNetCore2IastTestsFullSamplingEnabled : AspNetCore2IastTestsFullSampling
{
    public AspNetCore2IastTestsFullSamplingEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, testName: "AspNetCore2IastTestsEnabled", isIastDeduplicationEnabled: false, vulnerabilitiesPerRequest: 200)
    {
        SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    }

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
        var filename = "Iast.XContentTypeHeaderMissing.AspNetCore2." + contentType.Replace("/", string.Empty) +
            "." + returnCode.ToString() + "." + (string.IsNullOrEmpty(xContentTypeHeaderValue) ? "empty" : xContentTypeHeaderValue);
        var url = "/Iast/XContentTypeHeaderMissing" + queryParams;
        var isHtml = contentType.Contains("html") || contentType.Contains("xhtml");
        var expectVulnerability = isHtml && returnCode == 200 && !xContentTypeHeaderValue.Equals("nosniff", StringComparison.OrdinalIgnoreCase);
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, expectVulnerability ? filename : NotVulnerableSnapshotName, since: since, timeoutMs: expectVulnerability ? 5_000 : 1_000);
    }

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
        var filename = "Iast.StrictTransportSecurity.AspNetCore2." + contentType.Replace("/", string.Empty) +
            "." + returnCode.ToString() + "." + (string.IsNullOrEmpty(hstsHeaderValue) ? "empty" : hstsHeaderValue)
            + "." + (string.IsNullOrEmpty(xForwardedProto) ? "empty" : xForwardedProto);
        var url = "/Iast/StrictTransportSecurity" + queryParams;
        var isHtml = contentType.Contains("html") || contentType.Contains("xhtml");
        var isHttps = !string.IsNullOrEmpty(xForwardedProto);
        var isValidHsts = Regex.IsMatch(Uri.UnescapeDataString(hstsHeaderValue ?? string.Empty), @"^max-age=[1-9]\d*");
        var expectVulnerability = isHtml && isHttps && returnCode == 200 && !isValidHsts;
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, expectVulnerability ? filename : NotVulnerableSnapshotName, since: since, timeoutMs: expectVulnerability ? 5_000 : 1_000);
    }

    [Fact]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    public async Task TestStackTraceLeak()
    {
        var filename = "Iast.StackTraceLeak.AspNetCore2";
        var url = "/Iast/StackTraceLeak";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, [url]);

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastXpathInjectionRequest()
    {
        var filename = "Iast.XpathInjection.AspNetCore2";
        var url = "/Iast/XpathInjection?user=klaus&value=pass";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastEmailHtmlInjectionRequest()
    {
        var filename = "Iast.EmailHtmlInjection.AspNetCore2";
        var url = $"/Iast/SendEmail?email=alice@aliceland.com&name=Alice&lastname=Stevens&escape=false";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, [url]);

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }
}

public class AspNetCore2IastTestsFullSamplingRedactionEnabled : AspNetCore2IastTestsFullSampling
{
    public AspNetCore2IastTestsFullSamplingRedactionEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, isIastDeduplicationEnabled: false, testName: "AspNetCore2IastTestsRedactionEnabled", redactionEnabled: true, vulnerabilitiesPerRequest: 200)
    {
    }
}

public abstract class AspNetCore2IastTestsFullSampling : AspNetCore2IastTests
{
    public AspNetCore2IastTestsFullSampling(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, string testName, bool? isIastDeduplicationEnabled = null, int? vulnerabilitiesPerRequest = null, bool redactionEnabled = false)
        : base(fixture, outputHelper, testName: testName, samplingRate: 100, isIastDeduplicationEnabled: isIastDeduplicationEnabled, vulnerabilitiesPerRequest: vulnerabilitiesPerRequest, redactionEnabled: redactionEnabled)
    {
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastNotWeakRequest()
    {
        var url = "/Iast";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, NotVulnerableSnapshotName, since: since, timeoutMs: 1_000);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastWeakHashingRequest()
    {
        var filename = "Iast.WeakHashing.Vulns.AspNetCore2";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, ["/Iast/WeakHashing"]);

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestRequestBodyTaintingRazor()
    {
        var filename = "Iast.RequestBodyTestRazor.AspNetCore2";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/DataRazorIastPage";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, url, "property=Execute&property3=2&Property2=nonexisting.exe", 1, 1, string.Empty, "application/x-www-form-urlencoded", null);

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
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
        var hash = BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(body))).Replace("-", string.Empty).Substring(0, 8);
        var filename = $"Iast.RequestBodyTest.Vulns.AspNetCore2.{hash}";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/ExecuteQueryFromBodyQueryData";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, url, body, 1, 1, string.Empty, "application/json", null);

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [SkippableFact]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastSqlInjectionRequest()
    {
        var filename = "Iast.SqlInjection.AspNetCore2";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/SqlQuery?query=SELECT%20Surname%20from%20Persons%20where%20name%20=%20%27Vicent%27";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastCommandInjectionRequest()
    {
        var filename = "Iast.CommandInjection.AspNetCore2";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/ExecuteCommand?file=nonexisting.exe&argumentLine=arg1";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastSSRFRequest()
    {
        var filename = "Iast.SSRF.AspNetCore2";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/SSRF?host=localhost";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [Trait("Category", "LinuxUnsupported")]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastLdapRequest()
    {
        var filename = "Iast.Ldap.AspNetCore2";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/Ldap?userName=Babs Jensen";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastCookieTaintingRequest()
    {
        var filename = "Iast.CookieTainting.AspNetCore2";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/ExecuteCommandFromCookie";
        IncludeAllHttpSpans = true;
        AddCookies(new Dictionary<string, string>() { { "file", "file.txt" }, { "argumentLine", "arg1" } });
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [SkippableTheory]
    [InlineData("/Iast/SafeCookie")]
    [InlineData("/Iast/AllVulnerabilitiesCookie")]
    public async Task TestIastInsecureCookieRequest(string url)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var filename = $"Iast.Vulns.AspNetCore2.path ={sanitisedUrl}";
        var expectVulnerability = url.Contains("AllVulnerabilitiesCookie");
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, expectVulnerability ? filename : NotVulnerableSnapshotName, since: since, timeoutMs: expectVulnerability ? 5_000 : 1_000);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastPathTraversalRequest()
    {
        var filename = "Iast.PathTraversal.AspNetCore2";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { "/Iast/GetFileContent?file=nonexisting.txt" });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastWeakRandomnessRequest()
    {
        var filename = "Iast.WeakRandomness.AspNetCore2";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { "/Iast/WeakRandomness" });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [SkippableTheory]
    [Trait("RunOnWindows", "True")]
    [InlineData("Vuln.SensitiveValue", new string[] { "name", "myName", "value", ":bearer secret" }, null)]
    public async Task TestIastHeaderInjectionRequest(string testCase, string[] headers, string[] cookies, bool useValueFromOriginHeader = false)
    {
        var notVulnerable = testCase.StartsWith("notvulnerable", StringComparison.OrdinalIgnoreCase);
        var filename = "Iast.HeaderInjection.AspNetCore2." + (notVulnerable ? "NotVuln" : testCase) +
            (useValueFromOriginHeader ? ".origin" : string.Empty);
        if (!notVulnerable && RedactionEnabled is true) { filename += ".RedactionEnabled"; }
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

        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, 1, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, notVulnerable ? NotVulnerableSnapshotName : filename, since: since, timeoutMs: notVulnerable ? 1_000 : 5_000);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastReflectedXssRequest()
    {
        var filename = "Iast.ReflectedXss.Vulns.AspNetCore2";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/ReflectedXss?param=<b>RawValue</b>";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, 2, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastReflectedXssEscapedRequest()
    {
        var url = "/Iast/ReflectedXssEscaped?param=<b>RawValue</b>";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, 2, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, NotVulnerableSnapshotName, since: since, timeoutMs: 1_000);
    }
}

public class AspNetCore2IastTests50PctSamplingIastEnabled : AspNetCore2IastTests
{
    public AspNetCore2IastTests50PctSamplingIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, testName: "AspNetCore2IastTestsEnabled", isIastDeduplicationEnabled: false, vulnerabilitiesPerRequest: 100, samplingRate: 50)
    {
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastWeakHashingRequestSampling()
    {
        IncludeAllHttpSpans = true;
        await TryStartApp();

        var since1 = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, ["/Iast/WeakHashing"]);
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, "Iast.WeakHashing.Vulns.AspNetCore2.Sampling", since: since1, timeoutMs: 2_000);

        var since2 = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, ["/Iast/WeakHashing"]);
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, "Iast.WeakHashing.Vulns.AspNetCore2.Sampling.DisabledFlag", since: since2, timeoutMs: 2_000);

        var since3 = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, ["/Iast/WeakHashing"]);
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, "Iast.WeakHashing.Vulns.AspNetCore2.Sampling", since: since3, timeoutMs: 2_000);
    }

    protected override async Task TryStartApp()
    {
        EnableEvidenceRedaction(RedactionEnabled);
        DisableObfuscationQueryString();
        SetEnvironmentVariable(ConfigurationKeys.Iast.IsIastDeduplicationEnabled, IsIastDeduplicationEnabled?.ToString() ?? string.Empty);
        SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilitiesPerRequest, VulnerabilitiesPerRequest?.ToString() ?? string.Empty);
        SetEnvironmentVariable(ConfigurationKeys.Iast.RequestSampling, SamplingRate?.ToString() ?? string.Empty);
        SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilityLogPath, VulnerabilityLogPath);
        await Fixture.TryStartApp(this, enableSecurity: false, sendHealthCheck: false);
        SetHttpPort(Fixture.HttpPort);
    }
}

public abstract class AspNetCore2IastTests : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
    protected const string NotVulnerableSnapshotName = "Iast.NotVulnerable";
    private static readonly Regex PortScrubber = new(@"(https?://[^/:]+):\d+", RegexOptions.Compiled);

    public AspNetCore2IastTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, string testName, bool? isIastDeduplicationEnabled = null, int? samplingRate = null, int? vulnerabilitiesPerRequest = null, bool? redactionEnabled = false)
        : base("AspNetCore2", outputHelper, "/shutdown", testName: testName)
    {
        Fixture = fixture;
        fixture.SetOutput(outputHelper);
        RedactionEnabled = redactionEnabled;
        IsIastDeduplicationEnabled = isIastDeduplicationEnabled;
        VulnerabilitiesPerRequest = vulnerabilitiesPerRequest;
        SamplingRate = samplingRate;
        VulnerabilityLogPath = Path.Combine(LogDirectory, $"iast-vulns-{GetType().Name}.jsonl");
        SetEnvironmentVariable(ConfigurationKeys.AppSec.StackTraceEnabled, "false");
    }

    protected AspNetCoreTestFixture Fixture { get; }

    protected bool? RedactionEnabled { get; }

    protected bool? IsIastDeduplicationEnabled { get; }

    protected int? VulnerabilitiesPerRequest { get; }

    protected int? SamplingRate { get; }

    protected string VulnerabilityLogPath { get; }

    public override void Dispose()
    {
        base.Dispose();
        Fixture.SetOutput(null);
    }

    protected static async Task VerifyVulnerabilityRecordsAsync(string path, string fileName, DateTime? since = null, bool includeStack = false, Action<JObject> recordSanitizer = null, int timeoutMs = 5_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        List<JObject> records;
        do
        {
            records = VulnerabilityJsonl.ReadRecords(path, since);
            if (records.Count > 0)
            {
                break;
            }

            await Task.Delay(50);
        }
        while (DateTime.UtcNow < deadline);

        var sanitized = records
            .OrderBy(r => r["type"]?.Value<string>(), StringComparer.Ordinal)
            .ThenBy(r => r["hash"]?.Value<int>())
            .Select(r => SanitizeForVerification(r, includeStack))
            .ToList();

        if (recordSanitizer is not null)
        {
            foreach (var record in sanitized)
            {
                recordSanitizer(record);
            }
        }

        VerifyHelper.InitializeGlobalSettings();
        await Verifier.Verify(sanitized, new VerifySettings())
                      .UseFileName(fileName)
                      .DisableRequireUniquePrefix();
    }

    protected virtual async Task TryStartApp()
    {
        EnableEvidenceRedaction(RedactionEnabled);
        DisableObfuscationQueryString();
        SetEnvironmentVariable(ConfigurationKeys.Iast.IsIastDeduplicationEnabled, IsIastDeduplicationEnabled?.ToString() ?? string.Empty);
        SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilitiesPerRequest, VulnerabilitiesPerRequest?.ToString() ?? string.Empty);
        SetEnvironmentVariable(ConfigurationKeys.Iast.RequestSampling, SamplingRate?.ToString() ?? string.Empty);
        SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilityLogPath, VulnerabilityLogPath);
        await Fixture.TryStartApp(this, enableSecurity: false);
        SetHttpPort(Fixture.HttpPort);
    }

    private static JObject SanitizeForVerification(JObject record, bool includeStack)
    {
        record.Remove("timestamp");

        if (record["request"] is JObject request && request["url"] is JValue urlValue)
        {
            request["url"] = PortScrubber.Replace(urlValue.Value<string>() ?? string.Empty, "$1:00000");
        }

        if (record["location"] is JObject location)
        {
            location.Remove("line");

            if (!includeStack)
            {
                location.Remove("stack");
            }
            else if (location["stack"] is JArray stack)
            {
                foreach (var frame in stack.OfType<JObject>())
                {
                    frame.Remove("line");
                    frame.Remove("column");
                }
            }
        }

        return record;
    }
}
#endif
