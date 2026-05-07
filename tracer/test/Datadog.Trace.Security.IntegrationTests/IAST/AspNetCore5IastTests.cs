// <copyright file="AspNetCore5IastTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon.SimpleEmail.Model;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Security.IntegrationTests.IAST;
using Datadog.Trace.TestHelpers;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Newtonsoft.Json.Linq;
using VerifyTests;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast;

// Use this class to test common vulnerabilities
public class AspNetCore5IastTestsFullSamplingIastEnabled : AspNetCore5IastTestsFullSampling
{
    public AspNetCore5IastTestsFullSamplingIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, vulnerabilitiesPerRequest: 200, isIastDeduplicationEnabled: false, testName: "AspNetCore5IastTestsFullSamplingIastEnabled")
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
        var isHtml = contentType.Contains("html") || contentType.Contains("xhtml");
        var expectVulnerability = isHtml && returnCode == 200 && !xContentTypeHeaderValue.Equals("nosniff", StringComparison.OrdinalIgnoreCase);
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, expectVulnerability ? filename : NotVulnerableSnapshotName, since: since, timeoutMs: expectVulnerability ? 5_000 : 1_000);
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

        var hash = BitConverter.ToString(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(queryParams))).Replace("-", string.Empty).Substring(0, 8);
        var filename = $"Iast.StrictTransportSecurity.AspNetCore5.{hash}";
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
        var filename = "Iast.StackTraceLeak.AspNetCore5";
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
        var filename = "Iast.XpathInjection.AspNetCore5";
        var url = "/Iast/XpathInjection?user=klaus&value=pass";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastReflectionInjectionRequest()
    {
        var filename = "Iast.ReflectionInjection.AspNetCore5";
        const string type = "System.String";
        var url = $"/Iast/TypeReflectionInjection?type={type}";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, [url]);

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestNewtonsoftJsonParseTainting()
    {
        var filename = "Iast.NewtonsoftJsonParseTainting.AspNetCore5";
        var url = "/Iast/NewtonsoftJsonParseTainting?json={\"key\": \"value\"}";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, [url]);

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

#if !NETFRAMEWORK && NETCOREAPP3_1_OR_GREATER
    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestSystemTextJsonParseTainting()
    {
        var filename = "Iast.SystemTextJsonParseTainting.AspNetCore5";
        var url = "/Iast/SystemTextJsonParseTainting?json={\"key\": \"value\"}";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, [url]);

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }
#endif

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestCookieNameRequest()
    {
        var filename = "Iast.CookieName.AspNetCore5";
        var url = "/Iast/TestCookieName";
        AddCookies(new Dictionary<string, string>() { { "cookiename", "cookievalue" } });
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
        var filename = "Iast.EmailHtmlInjection.AspNetCore5";
        var url = $"/Iast/SendEmail?email=alice@aliceland.com&name=Alice&lastname=Stevens&escape=false";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, [url]);

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [Theory]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "ArmUnsupported")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TestDatabaseSourceInjections(bool injectOnlyDatabase)
    {
        var filename = "Iast.DatabaseSourceInjection.AspNetCore5.Mixed";
        var url = $"/Iast/DatabaseSourceInjection?host=localhost&injectOnlyDatabase={injectOnlyDatabase}";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, [url]);

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, injectOnlyDatabase ? NotVulnerableSnapshotName : filename, since: since);
    }

    [SkippableTheory]
    [Trait("RunOnWindows", "True")]
    [InlineData(-1, 10)]
    [InlineData(-1, 15)]
    [InlineData(15, 15)]
    [InlineData(5, 15)]
    public async Task TestMaxRanges(int maxRanges, int nbrRangesCreated)
    {
        // Set the configuration (use default configuration if -1 is passed)
        var maxRangesConfiguration = maxRanges == -1 ? IastSettings.MaxRangeCountDefault : maxRanges;
        SetEnvironmentVariable(ConfigurationKeys.Iast.MaxRangeCount, maxRangesConfiguration.ToString());

        var filename = "Iast.MaxRanges.AspNetCore5." + maxRangesConfiguration + "." + nbrRangesCreated;
        var url = "/Iast/MaxRanges?count=" + nbrRangesCreated + "&tainted=taintedString|";

        IncludeAllHttpSpans = true;

        // Using a new fixture here to use a new process that applies
        // correctly the new environment variable value that is changing between tests
        var newFixture = new AspNetCoreTestFixture();
        newFixture.SetOutput(Output);
        await TryStartApp(newFixture);

        var since = DateTime.UtcNow;
        await SendRequestsAsync(newFixture.Agent, [url]);

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);

        newFixture.Dispose();
        newFixture.SetOutput(null);
    }

    [Fact]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    public async Task TestQueryParameterNameVulnerability()
    {
        var filename = "Iast.QueryParameterName.AspNetCore5";
        var url = "/Iast/Print?Encrypt=True&ClientDatabase=774E4D65564946426A53694E48756B592B444A6C43673D3D&p=413&ID=2376&EntityType=114&Print=True&OutputType=WORDOPENXML&SSRSReportID=1";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, [url]);

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

#if NET6_0_OR_GREATER
    [SkippableFact]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastSqliInterpolatedString()
    {
        var filename = "Iast.SqliInterpolatedString.AspNetCore5";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = $"/Iast/InterpolatedSqlString?name=John";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, 2, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }
#endif
}

// Classes to test particular features
public class AspNetCore5IastTestsStackTraces : AspNetCore5IastTests
{
    public AspNetCore5IastTestsStackTraces(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, testName: "AspNetCore5IastTestsStackTraces", samplingRate: 100, isIastDeduplicationEnabled: false, vulnerabilitiesPerRequest: 200, redactionEnabled: true)
    {
        SetEnvironmentVariable(ConfigurationKeys.AppSec.StackTraceEnabled, "true");
        SetEnvironmentVariable(ConfigurationKeys.AppSec.MaxStackTraceDepth, "1");
    }

    [SkippableTheory]
    [Trait("RunOnWindows", "True")]
    [InlineData("Vulnerability.WithoutLocation", "/Iast/InsecureCookie")]
    [InlineData("Vulnerability.InFunction", "/Iast/GetFileContent?file=nonexisting.txt")]
    [InlineData("Vulnerability.LocatedDeeper", "/Iast/WeakHashing")]
    [InlineData("Vulnerability.LocatedInRenderPipeline", "/Iast/ReflectedXss?param=<b>RawValue</b>")]
    public async Task TestVulnerabilityStack(string name, string url)
    {
        var fileName = "Iast.Stacks." + name;

        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, fileName, since: since, includeStack: true);
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
}

public abstract class AspNetCore5IastTestsVariableVulnerabilityPerRequestIastEnabled : AspNetCore5IastTests
{
    public AspNetCore5IastTestsVariableVulnerabilityPerRequestIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, int vulnerabilitiesPerRequest)
        : base(fixture, outputHelper, testName: "AspNetCore5IastTestsVariableVulnerabilityPerRequestIastEnabled", isIastDeduplicationEnabled: false, samplingRate: 100, vulnerabilitiesPerRequest: vulnerabilitiesPerRequest)
    {
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastWeakHashingRequestVulnerabilitiesPerRequest()
    {
        var filename = VulnerabilitiesPerRequest == 1 ? "Iast.WeakHashing.Vulns.AspNetCore5.SingleVulnerability" : "Iast.WeakHashing.Vulns.AspNetCore5";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, ["/Iast/WeakHashing"]);

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }
}

public class AspNetCore5IastTestsRestartedSampleIastEnabled : AspNetCore5IastTests
{
    public AspNetCore5IastTestsRestartedSampleIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, vulnerabilitiesPerRequest: 200, isIastDeduplicationEnabled: false, testName: "AspNetCore5IastTestsRestartedSampleIastEnabled", redactionEnabled: true, samplingRate: 100)
    {
    }

    [SkippableTheory]
    [InlineData("IAST_TEST_ENABLE_DIRECTORY_LISTING_REQUEST_PATH")]
    [InlineData("IAST_TEST_ENABLE_DIRECTORY_LISTING_WHOLE_APP")]
    [InlineData("IAST_TEST_ENABLE_DIRECTORY_LISTING_STRING_PATH")]
    [Trait("RunOnWindows", "True")]
    public async Task TestDirectoryListingLeak(string featureEnvVar)
    {
        SetEnvironmentVariable(featureEnvVar, "true");

        var filename = "Iast.DirectoryListingLeak.AspNetCore5";
        var newFixture = new AspNetCoreTestFixture();
        newFixture.SetOutput(Output);

        var since = DateTime.UtcNow;
        await TryStartApp(newFixture);

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);

        newFixture.Dispose();
        newFixture.SetOutput(null);
    }

    [SkippableTheory]
    [InlineData(31)]
    [InlineData(120)]
    [Trait("RunOnWindows", "True")]
    public async Task TestSessionTimeoutVulnerability(int timeoutMinutes)
    {
        SetEnvironmentVariable("IAST_TEST_SESSION_IDLE_TIMEOUT", timeoutMinutes.ToString());

        var filename = "Iast.SessionIdleTimeout.AspNetCore5";
        var newFixture = new AspNetCoreTestFixture();
        newFixture.SetOutput(Output);

        var since = DateTime.UtcNow;
        await TryStartApp(newFixture);

        await VerifyVulnerabilityRecordsAsync(
            VulnerabilityLogPath,
            filename,
            since: since,
            recordSanitizer: record =>
            {
                // Normalize the timeout value (31 or 120 minutes) so both InlineData cases share one snapshot.
                if (record["evidence"] is JObject ev && ev["value"] is JValue val)
                {
                    ev["value"] = Regex.Replace(val.Value<string>() ?? string.Empty, @"\d+ minutes", "XXX minutes");
                }

                // net5.0 uses different path/method for the startup entry point — normalise.
                if (record["location"] is JObject loc)
                {
                    if (loc["path"]?.Value<string>() == "Samples.Security.AspNetCore5.Program")
                    {
                        loc["path"] = "Samples.Security.AspNetCore5.Startup+<>c__DisplayClass4_0";
                    }

                    if (loc["method"]?.Value<string>() == "Main")
                    {
                        loc["method"] = "<ConfigureServices>b__0";
                    }
                }
            });

        newFixture.Dispose();
        newFixture.SetOutput(null);
    }
}

public class AspNetCore5IastTestsFullSamplingRedactionEnabled : AspNetCore5IastTestsFullSampling
{
    public AspNetCore5IastTestsFullSamplingRedactionEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, isIastDeduplicationEnabled: false, testName: "AspNetCore5IastTestsRedactionEnabled", redactionEnabled: true, vulnerabilitiesPerRequest: 200)
    {
    }
}

[Collection(nameof(AspNetCore5IastTestsFullSampling))]
[CollectionDefinition(nameof(AspNetCore5IastTestsFullSampling), DisableParallelization = true)]
public abstract class AspNetCore5IastTestsFullSampling : AspNetCore5IastTests
{
    public AspNetCore5IastTestsFullSampling(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, string testName, bool? isIastDeduplicationEnabled = null, int? vulnerabilitiesPerRequest = null, bool redactionEnabled = false)
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
        var filename = "Iast.WeakHashing.Vulns.AspNetCore5";
        var url = "/Iast/WeakHashing";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestRequestBodyTaintingRazor()
    {
        var filename = "Iast.RequestBodyTestRazor.AspNetCore5";
        var url = "/DataRazorIastPage";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
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
        var hash = BitConverter.ToString(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(body))).Replace("-", string.Empty).Substring(0, 8);
        var filename = $"Iast.RequestBodyTest.Vulns.AspNetCore5.{hash}";
        var url = "/Iast/ExecuteQueryFromBodyQueryData";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
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
        var filename = "Iast.SqlInjection.Vulns.AspNetCore5";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/SqlQuery?username=Vicent";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastNoSqlMongoDbInjectionRequest()
    {
        var filename = "Iast.NoSqlMongoDbInjection.Vulns.AspNetCore5";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        const string value = "1\", \"$or\": [{\"Price\": {\"$gt\": 1000}}], \"other\": \"1";
        var url = $"/Iast/NoSqlQueryMongoDb?price={value}";
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
        var filename = "Iast.CommandInjection.Vulns.AspNetCore5";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/ExecuteCommand?file=nonexisting.exe&argumentLine=arg1";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    // Proof-of-concept: this test asserts on the IAST vulnerability JSON-lines
    // report file written by the sample app instead of going through agent spans
    // and snapshots. The output path is set per-fixture via DD_IAST_VULNERABILITY_LOG_PATH.
    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastSSRFRequest()
    {
        var filename = "Iast.SSRF.Vulns.AspNetCore5";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }

        var url = "/Iast/SSRF?host=localhost";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [SkippableFact]
    [Trait("Category", "LinuxUnsupported")]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastLdapRequest()
    {
        var filename = "Iast.Ldap.Vulns.AspNetCore5";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/Ldap?path=LDAP://ldap.forumsys.com:389/dc=example,dc=com";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { url });

        // The local-function name (<Ldap>g__PerformLdapQuery|N_M) and the resulting
        // hash drift across compiler versions; SanitizeLdap normalises both before
        // the snapshot.
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since, recordSanitizer: SanitizeLdap);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastHeaderTaintingRequest()
    {
        var filename = "Iast.HeaderTainting.Vulns.AspNetCore5";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/ExecuteCommandFromHeader";
        IncludeAllHttpSpans = true;
        AddHeaders(new() { { "file", "file.txt" }, { "argumentLine", "arg1" } });
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastCookieTaintingRequest()
    {
        var filename = "Iast.CookieTainting.Vulns.AspNetCore5";
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
    [InlineData("/Iast/SafeCookie", false)]
    [InlineData("/Iast/AllVulnerabilitiesCookie", true)]
    public async Task TestIastCookiesRequest(string url, bool vulnerable)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var filename = $"Iast.Vulns.AspNetCore5.path ={sanitisedUrl}";
        var cookieTypes = new[] { "INSECURE_COOKIE", "NO_HTTPONLY_COOKIE", "NO_SAMESITE_COOKIE" };
        // SafeCookie hits a controller that emits non-vulnerable cookies; only
        // /Iast/AllVulnerabilitiesCookie should produce records.
        var expectVulnerability = url.Contains("AllVulnerabilitiesCookie");
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, vulnerable ? filename : NotVulnerableSnapshotName, since: since, timeoutMs: expectVulnerability ? 5_000 : 1_000);
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [SkippableTheory]
    [InlineData("BasicAuth", "Authorization", "Basic QWxhZGRpbjpvcGVuIHNlc2FtZQ==")]
    [InlineData("BasicAuth", "Authorization", "basic QWxhZGRpbjpvcGVuIHNlc2FtZQ==")]
    [InlineData("BasicAuth", "Authorization", "    bAsic    QWxhZGRpbjpvcGVuIHNlc2FtZQ==")]
    [InlineData("DigestAuth", "Authorization", "digest realm=\"testrealm@host.com\", qop=\"auth,auth-int\", nonce=\"dcd98b7102dd2f0e8b11d0f600bfb0c093\", opaque=\"5ccc069c403ebaf9f0171e9517f40e41\"")]
    public async Task TestIastInsecureAuthProtocolRequest(string name, string header, string data)
    {
        var filename = "Iast.InsecureAuthProtocol.Vulns.AspNetCore5." + name;
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }

        var url = "/Iast/InsecureAuthProtocol";
        IncludeAllHttpSpans = true;
        AddHeaders(new Dictionary<string, string> { { header, data } });
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, [url]);

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastPathTraversalRequest()
    {
        var filename = "Iast.PathTraversal.Vulns.AspNetCore5";
        var url = "/Iast/GetFileContent?file=nonexisting.txt";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastWeakRandomnessRequest()
    {
        var filename = "Iast.WeakRandomness.Vulns.AspNetCore5";
        var url = "/Iast/WeakRandomness";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastHardcodedSecretsRequest()
    {
        var filename = "Iast.HardcodedSecrets.Vulns.AspNetCore5";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/HardcodedSecrets";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, 6, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastTrustBoundaryViolationRequest()
    {
        var filename = "Iast.TrustBoundaryViolation.Vulns.AspNetCore5";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/Tbv?name=name&value=value";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, 1, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastUnvalidatedRedirectRequest()
    {
        var filename = "Iast.UnvalidatedRedirect.Vulns.AspNetCore5";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/UnvalidatedRedirect?param=value";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, 4, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastReflectedXssRequest()
    {
        var filename = "Iast.ReflectedXss.Vulns.AspNetCore5";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/ReflectedXss?param=<b>RawValue</b>";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, 2, new[] { url });

        // The XSS handler is generated by the Razor compiler — its synthesized
        // type/method names drift across compiler versions, so we normalise the
        // path and pin the hash for snapshot stability.
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since, recordSanitizer: SanitizeReflectedXss);
    }

    [SkippableTheory]
    [InlineData("RawValue")]
    [InlineData("<script>alert('XSS')</script>")]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastReflectedXssEscapedRequest(string param)
    {
        var url = "/Iast/ReflectedXssEscaped?param=" + param;
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, 2, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, NotVulnerableSnapshotName, since: since, timeoutMs: 1_000);
    }

    // In header injections, we should exclude some headers to prevent false positives:
    // location: it is already reported in UNVALIDATED_REDIRECT vulnerability detection.
    // Sec-WebSocket-Location, Sec-WebSocket-Accept, Upgrade, Connection: Usually the framework gets info from request
    // access-control-allow-*: when the source of the tainted range is the request header origin or access-control-request-headers
    // set-cookie: We should ignore set-cookie header if the source of all the tainted ranges are cookies
    // "vary: origin"
    // We should exclude the injection when the tainted string only has one range which comes from a request header with the same name that the header that we are checking in the response.
    // Headers could store sensitive information, we should redact whole <header_value> if:
    // <header_name> matches with a RegExp
    // <header_value> matches with  a RegExp
    // We should redact the sensitive information from the evidence when:
    // Tainted range is considered sensitive value

    [Trait("Category", "EndToEnd")]
    [SkippableTheory]
    [Trait("RunOnWindows", "True")]
    [InlineData("Vuln.SensitiveName", new string[] { "name", "private_token" }, new string[] { "value", "ShouldBeRedacted" })]
    [InlineData("Vuln.SensitiveValue", new string[] { "name", "myName", "value", ":bearer secret" }, null)]
    [InlineData("Vuln.SensitiveValueComplex", new string[] { "name", "myName", "value", ":bear" }, new string[] { "value", "er%20secret" })]
    [InlineData("NotVulnerable", new string[] { "propagation", "noVulnValue" }, null)]
    [InlineData("Vuln.NoSensitive", new string[] { "name", "Name", "value", "value" }, new string[] { "value", "moreText" })]
    [InlineData("NotVulnerable", new string[] { "name", "Sec-WebSocket-Accept" }, new string[] { "value", "moreText" })]
    [InlineData("Vuln.Origin", new string[] { "name", "access-control-allow-origin", "value", "https://example.com" }, null)]
    [InlineData("NotVulnerable", new string[] { "name", "access-control-allow-origin", "origin", "NotVulnerable" }, null, true)] // Not vulnerable
    [InlineData("NotVulnerable", new string[] { "name", "Access-Control-Allow-Headers", "Access-Control-Request-Headers", "NotVulnerable" }, null, true)] // Not vulnerable
    [InlineData("Vuln.Cookie.SensitiveValue", new string[] { "name", "set-cookie", "value", "token=glpat-eFynewhuKJFGdfGDFGdw;max-age=31536000;Secure;HttpOnly;SameSite=Strict" }, null)]
    [InlineData("NotVulnerable", null, new string[] { "name", "set-cookie", "value", "NotVulnerable%3D22%3Bmax-age%3D31536000%3BSecure%3BHttpOnly%3BSameSite%3DStrict" })]
    [InlineData("Vuln.MultipleHeaderValues", new string[] { "name", "extraName", "value", "value2" }, null)]
    public async Task TestIastHeaderInjectionRequest(string testCase, string[] headers, string[] cookies, bool useValueFromOriginHeader = false)
    {
        var notVulnerable = testCase.StartsWith("notvulnerable", StringComparison.OrdinalIgnoreCase);
        var filename = "Iast.HeaderInjection.AspNetCore5." + testCase +
            (useValueFromOriginHeader ? ".origin" : string.Empty);
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
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
    public async Task TestNHibernateSqlInjection()
    {
        var filename = "Iast.NHibernateSqlInjection.Vulns.AspNetCore5";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/NHibernateQuery?username=TestUser";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, [url]);

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since);
    }

    private static void SanitizeLdap(JObject record)
    {
        // Compiler-generated method names for local functions are unstable across
        // compiler versions; normalise the suffix and any nested method paths too.
        if (record["location"] is JObject location)
        {
            if (location["method"]?.Value<string>() is { } method && method.StartsWith("<Ldap>g__PerformLdapQuery|"))
            {
                location["method"] = "<Ldap>g__PerformLdapQuery|0";
            }

            if (location["path"]?.Value<string>() is { } path && path.StartsWith("Samples.Security.AspNetCore5.Controllers.IastController+"))
            {
                location["path"] = "Samples.Security.AspNetCore5.Controllers.IastController+";
            }
        }

        // Hash includes the (path, method) tuple, so it drifts when those drift —
        // pin to a constant for snapshot stability.
        record["hash"] = 9515978;
    }

    private static void SanitizeReflectedXss(JObject record)
    {
        if (record["location"] is JObject location && location["path"]?.Value<string>() is { } path)
        {
            // Match the legacy regex `\"path\": \"AspNetCore[^\\.]+\\.` → `AspNetCore.`
            const string prefix = "AspNetCore";
            var dot = path.IndexOf('.');
            if (path.StartsWith(prefix, StringComparison.Ordinal) && dot > 0)
            {
                location["path"] = "AspNetCore" + path.Substring(dot);
            }
        }

        record["hash"] = "XXX";
    }
}

public abstract class AspNetCore5IastTests : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
    protected const string NotVulnerableSnapshotName = "Iast.NotVulnerable";
#pragma warning disable SA1311 // Static readonly fields should begin with upper-case letter
    protected static readonly (Regex RegexPattern, string Replacement) aspNetCorePathScrubber = (new Regex("\"path\": \"AspNetCore[^\\.]+\\."), "\"path\": \"AspNetCore.");
    protected static readonly (Regex RegexPattern, string Replacement) hashScrubber = (new Regex("\"hash\": .+,"), "\"hash\": XXX,");
    protected static readonly Regex PortScrubber = new(@"(https?://[^/:]+):\d+", RegexOptions.Compiled);
#pragma warning restore SA1311 // Static readonly fields should begin with upper-case letter

    public AspNetCore5IastTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, string testName, bool? isIastDeduplicationEnabled = null, int? samplingRate = null, int? vulnerabilitiesPerRequest = null, bool? redactionEnabled = false, string sampleName = "AspNetCore5")
        : base(sampleName, outputHelper, "/shutdown", testName: testName)
    {
        Fixture = fixture;
        fixture.SetOutput(outputHelper);
        IsIastDeduplicationEnabled = isIastDeduplicationEnabled;
        VulnerabilitiesPerRequest = vulnerabilitiesPerRequest;
        SamplingRate = samplingRate;
        RedactionEnabled = redactionEnabled;

        // Per-class path for the IAST vulnerability JSONL report, placed alongside
        // other tracer logs. Derived from the concrete type name so it is stable
        // across all xUnit theory instances of the same class (xUnit creates a new
        // instance per InlineData case, but all share the same fixture and app process,
        // which is started once with this path and keeps writing here).
        VulnerabilityLogPath = Path.Combine(LogDirectory, $"iast-vulns-{GetType().Name}.jsonl");

        SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        SetEnvironmentVariable(ConfigurationKeys.AppSec.StackTraceEnabled, "false");
        UseNativeLibraryAlpineWorkaround();
    }

    protected AspNetCoreTestFixture Fixture { get; }

    protected bool? RedactionEnabled { get; }

    protected bool? IsIastDeduplicationEnabled { get; }

    protected int? VulnerabilitiesPerRequest { get; }

    protected int? SamplingRate { get; }

    /// <summary>
    /// Gets the path to the IAST vulnerability JSON-lines report file written by the sample app.
    /// Unique per fixture instance so parallel test classes don't collide.
    /// </summary>
    protected string VulnerabilityLogPath { get; }

    public override void Dispose()
    {
        base.Dispose();
        Fixture.SetOutput(null);
    }

    public virtual async Task TryStartApp(MockTracerAgent.AgentConfiguration agentConfiguration = null)
    {
        await TryStartApp(Fixture, agentConfiguration);
    }

    public virtual async Task TryStartApp(AspNetCoreTestFixture fixture, MockTracerAgent.AgentConfiguration agentConfiguration = null)
    {
        EnableEvidenceRedaction(RedactionEnabled);
        DisableObfuscationQueryString();
        SetEnvironmentVariable(ConfigurationKeys.Iast.IsIastDeduplicationEnabled, IsIastDeduplicationEnabled?.ToString() ?? string.Empty);
        SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilitiesPerRequest, VulnerabilitiesPerRequest?.ToString() ?? string.Empty);
        SetEnvironmentVariable(ConfigurationKeys.Iast.RequestSampling, SamplingRate?.ToString() ?? string.Empty);
        SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilityLogPath, VulnerabilityLogPath);

        if (File.Exists(VulnerabilityLogPath))
        {
            try
            {
                File.Delete(VulnerabilityLogPath);
            }
            catch { }
        }

        await fixture.TryStartApp(this, enableSecurity: false, agentConfiguration: agentConfiguration);
        SetHttpPort(fixture.HttpPort);
    }

    protected static async Task VerifyVulnerabilityRecordsAsync(string path, string fileName, DateTime? since = null, bool includeStack = false, Action<JObject> recordSanitizer = null, int timeoutMs = 5_000)
    {
        // Poll until at least one record appears or the timeout expires. A short
        // timeoutMs (e.g. 1_000) is appropriate for tests that expect no records.
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        List<JObject> records;
        do
        {
            records = TryReadRecords(path, since);
            if (records.Count > 0)
            {
                break;
            }

            await Task.Delay(50);
        }
        while (DateTime.UtcNow < deadline);

        // Stable order across runs: snapshots compare line-by-line, so we sort by type then hash.
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

        // Land snapshots in tracer/test/snapshots/ alongside the existing span
        // snapshots. VerifyHelper sets this globally for all Verify calls.
        VerifyHelper.InitializeGlobalSettings();

        var settings = new VerifySettings();
        await Verifier.Verify(sanitized, settings)
                      .UseFileName(fileName)
                      .DisableRequireUniquePrefix();
    }

    private static JObject SanitizeForVerification(JObject record, bool includeStack)
    {
        // Drop volatile fields so the snapshot stays stable across runs and code edits.
        record.Remove("timestamp");

        if (record["request"] is JObject request && request["url"] is JValue urlValue)
        {
            request["url"] = PortScrubber.Replace(urlValue.Value<string>() ?? string.Empty, "$1:00000");
        }

        if (record["location"] is JObject location)
        {
            // Line numbers are present in debug builds (PDB info) but absent in
            // release — remove rather than replace so both produce the same snapshot.
            location.Remove("line");

            if (!includeStack)
            {
                // Stack frames vary by framework version and instrumentation depth,
                // so most tests exclude them. Stack-specific tests can opt in.
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

    private static List<JObject> TryReadRecords(string path, DateTime? since)
    {
        var matches = new List<JObject>();
        if (!File.Exists(path))
        {
            return matches;
        }

        // Open shared with write so the sample app can keep appending while we read.
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);
        string line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JObject record;
            try
            {
                record = JObject.Parse(line);
            }
            catch
            {
                continue;
            }

            if (since is { } cutoff && (record["timestamp"]?.Value<DateTime?>() is not { } emitted || emitted < cutoff))
            {
                continue;
            }

            matches.Add(record);
        }

        return matches;
    }
}

#endif
