// <copyright file="AspNetCore5IastTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Security.IntegrationTests.IAST;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
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
        : base(fixture, outputHelper, vulnerabilitiesPerRequest: 3, isIastDeduplicationEnabled: false, testName: "AspNetCore5IastTestsFullSamplingIastEnabled")
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
        settings.AddIastScrubbing();
        await VerifySpans(spans, settings, fileNameOverride: filename);
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
        settings.AddIastScrubbing();
        await VerifySpans(spans, settings, fileNameOverride: filename);
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
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, [url]);

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifySpans(spans, settings, fileNameOverride: filename);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastXpathInjectionRequest()
    {
        var filename = "Iast.XpathInjection.AspNetCore5";
        var url = "/Iast/XpathInjection?user=klaus&value=pass";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToImmutableList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifySpans(spansFiltered, settings, fileNameOverride: filename);
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
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, [url]);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToImmutableList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifySpans(spansFiltered, settings, fileNameOverride: filename);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestNewtonsoftJsonParseTainting()
    {
        var filename = "Iast.NewtonsoftJsonParseTainting.AspNetCore5";
        var url = "/Iast/NewtonsoftJsonParseTainting?json={\"key\": \"value\"}";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, [url]);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToImmutableList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifySpans(spansFiltered, settings, fileNameOverride: filename);
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
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, [url]);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToImmutableList();
        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifySpans(spansFiltered, settings, fileNameOverride: filename);
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
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToImmutableList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifySpans(spansFiltered, settings, fileNameOverride: filename);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastEmailHtmlInjectionRequest()
    {
        var filename = "Iast.EmailHtmlInjection.AspNetCore5";
        var url = $"/Iast/SendEmail?email=alice@aliceland.com&name=Alice&lastname=Stevens&escape=false";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, [url]);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToImmutableList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifySpans(spansFiltered, settings, fileNameOverride: filename);
    }

    [Theory]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "ArmUnsupported")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TestDatabaseSourceInjections(bool injectOnlyDatabase)
    {
        var filename = "Iast.DatabaseSourceInjection.AspNetCore5." + (injectOnlyDatabase ? "DbOnly" : "Mixed");
        var url = $"/Iast/DatabaseSourceInjection?host=localhost&injectOnlyDatabase={injectOnlyDatabase}";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, [url]);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToImmutableList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifySpans(spansFiltered, settings, fileNameOverride: filename);
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

        var agent = newFixture.Agent;
        var spans = await SendRequestsAsync(agent, [url]);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToImmutableList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifySpans(spansFiltered, settings, fileNameOverride: filename);

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
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, [url]);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToImmutableList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifySpans(spansFiltered, settings, fileNameOverride: filename);
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
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, 2, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web || x.Type == SpanTypes.IastVulnerability).ToImmutableList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifySpans(spansFiltered, settings, fileNameOverride: filename);
    }
    #endif

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastEventMetaStructEnabled()
    {
        var filename = "Iast.MetaStruct.AspNetCore5";
        const string type = "System.String";
        var url = $"/Iast/TypeReflectionInjection?type={type}";
        IncludeAllHttpSpans = true;

        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, [url]);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToImmutableList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing(forceMetaStruct: true);

        await VerifySpans(spansFiltered, settings, fileNameOverride: filename);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastVulnerabilitySampling()
    {
        var filename = "Iast.VulnerabilitySampling.AspNetCore5";
        var url1 = $"/Iast/Sampling1";
        var url2 = $"/Iast/Sampling2";
        IncludeAllHttpSpans = true;

        // Each route has 3 vulnerabilities (as the budget)
        // First call to the route will rend 3 vulns, depleting budget.
        // Second call will render none (sampling mechanism) and will reset as the budget was not depleted this time
        // Third call will render all 3 vulns again (as the budget was reset)
        // The same behabiour is repeated for the second route
        // Calls are interleaved to test the stats persistency of each route

        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, [url1, url2, url1, url2, url1, url2]);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web && x.Name == "aspnet_core.request").ToImmutableList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing(forceMetaStruct: true);

        await VerifySpans(spansFiltered, settings, fileNameOverride: filename);
    }
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
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        var hashRegex = (new Regex(@"""hash"": -?\d+"), @"""hash"": XXX");
        var pathRegex = (new Regex(@"""path"": ""AspNetCore.*\."), @"""path"": ""AspNetCore.");

        settings.AddRegexScrubber(hashRegex);
        settings.AddRegexScrubber(pathRegex);

        foreach (var span in spans)
        {
            if (span.MetaStruct is not null)
            {
                if (span.MetaStruct.TryGetValue("_dd.stack", out var data))
                {
                    var json = MetaStructToJson(data);
                    span.Tags["_dd.stack"] = json;
                }
            }
        }

        await VerifySpans(spans, settings, fileNameOverride: fileName);
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
        var url = "/Iast/WeakHashing2";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });
        var parentSpan = spans.First(x => x.ParentId == null);
        IastVerifyScrubberExtensions.IastMetaStructScrubbing(parentSpan);
        var childSpan = spans.First(x => x.ParentId == parentSpan.SpanId);
        var vulnerabilityJson = parentSpan.GetTag(Tags.IastJson);
        vulnerabilityJson.Should().Contain("\"spanId\": " + childSpan.SpanId);
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
        var filename = VulnerabilitiesPerRequest == 1 ? "Iast.WeakHashing.AspNetCore5.SingleVulnerability" : "Iast.WeakHashing.AspNetCore5";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        await TestWeakHashing(filename, Fixture.Agent);
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

        var datetimeOffset = DateTimeOffset.UtcNow; // Catch vulnerability at the startup of the app
        await TryStartApp(newFixture, new MockTracerAgent.AgentConfiguration { SpanMetaStructs = false });

        var agent = newFixture.Agent;
        var spans = await agent.WaitForSpansAsync(1, minDateTime: datetimeOffset);

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifySpans(spans, settings, fileNameOverride: filename);

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

        var datetimeOffset = DateTimeOffset.UtcNow; // Catch vulnerability at the startup of the app
        await TryStartApp(newFixture, new MockTracerAgent.AgentConfiguration { SpanMetaStructs = false });

        var agent = newFixture.Agent;
        var spans = await agent.WaitForSpansAsync(1, minDateTime: datetimeOffset);

        // Add a scrubber for "Session idle timeout is configured with: options.IdleTimeout, with a value of x minutes" and also for the hash value
        (Regex RegexPattern, string Replacement) sessionIdleTimeoutRegex = (new Regex(@"Session idle timeout is configured with: options.IdleTimeout, with a value of \d+ minutes"), "Session idle timeout is configured with: options.IdleTimeout, with a value of XXX minutes");
        (Regex RegexPattern, string Replacement) hashRegex = (new Regex(@"""hash"": -?\d+"), @"""hash"": XXX");

        // Only for net5.0: path and method are different
        (Regex RegexPattern, string Replacement) pathRegex = (new Regex(@"""path"": ""Samples.Security.AspNetCore5.Program"""), @"""path"": ""Samples.Security.AspNetCore5.Startup+<>c__DisplayClass4_0""");
        (Regex RegexPattern, string Replacement) methodRegex = (new Regex(@"""method"": ""Main"""), @"""method"": ""<ConfigureServices>b__0""");

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        settings.AddRegexScrubber(sessionIdleTimeoutRegex);
        settings.AddRegexScrubber(hashRegex);
        settings.AddRegexScrubber(pathRegex);
        settings.AddRegexScrubber(methodRegex);

        await VerifySpans(spans, settings, fileNameOverride: filename);

        newFixture.Dispose();
        newFixture.SetOutput(null);
    }
}

public class AspNetCore5IastTestsFullSamplingRedactionEnabled : AspNetCore5IastTestsFullSampling
{
    public AspNetCore5IastTestsFullSamplingRedactionEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, isIastDeduplicationEnabled: false, testName: "AspNetCore5IastTestsRedactionEnabled", redactionEnabled: true, vulnerabilitiesPerRequest: 3)
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
        var filename = "Iast.NotWeak.AspNetCore5";
        var url = "/Iast";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifySpans(spans, settings, fileNameOverride: filename);
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

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, "WEAK_HASH", filename, expectVulnerability: true, since: since);
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
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, url, "property=Execute&property3=2&Property2=nonexisting.exe", 1, 1, string.Empty, "application/x-www-form-urlencoded", null);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToImmutableList();
        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifySpans(spansFiltered, settings, fileNameOverride: filename);
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
        var filename = "Iast.RequestBodyTest.Vulns.AspNetCore5";
        var url = "/Iast/ExecuteQueryFromBodyQueryData";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, url, body, 1, 1, string.Empty, "application/json", null);

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, "SQL_INJECTION", filename, expectVulnerability: true, since: since);
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

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, "SQL_INJECTION", filename, expectVulnerability: true, since: since);
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

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, "NOSQL_MONGODB_INJECTION", filename, expectVulnerability: true, since: since);
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

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, "COMMAND_INJECTION", filename, expectVulnerability: true, since: since);
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

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, "SSRF", filename, expectVulnerability: true, since: since);
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
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, "LDAP_INJECTION", filename, expectVulnerability: true, since: since, recordSanitizer: SanitizeLdap);
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

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, "COMMAND_INJECTION", filename, expectVulnerability: true, since: since);
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

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, "COMMAND_INJECTION", filename, expectVulnerability: true, since: since);
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [SkippableTheory]
    [InlineData("/Iast/SafeCookie")]
    [InlineData("/Iast/AllVulnerabilitiesCookie")]
    public async Task TestIastCookiesRequest(string url)
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

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, cookieTypes, filename, expectVulnerability: expectVulnerability, since: since);
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

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, "INSECURE_AUTH_PROTOCOL", filename, expectVulnerability: true, since: since);
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

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, "PATH_TRAVERSAL", filename, expectVulnerability: true, since: since);
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

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, "WEAK_RANDOMNESS", filename, expectVulnerability: true, since: since);
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

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, "HARDCODED_SECRET", filename, expectVulnerability: true, since: since);
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

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, "TRUST_BOUNDARY_VIOLATION", filename, expectVulnerability: true, since: since);
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

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, "UNVALIDATED_REDIRECT", filename, expectVulnerability: true, since: since);
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
        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, "XSS", filename, expectVulnerability: true, since: since, recordSanitizer: SanitizeReflectedXss);
    }

    [SkippableTheory]
    [InlineData("RawValue")]
    [InlineData("<script>alert('XSS')</script>")]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastReflectedXssEscapedRequest(string param)
    {
        var filename = "Iast.ReflectedXssEscaped.AspNetCore5";
        var url = "/Iast/ReflectedXssEscaped?param=" + param;
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, 2, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web || x.Type == SpanTypes.IastVulnerability).ToImmutableList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();

        // Add a scrubber to remove the "?param=<value>"
        (Regex RegexPattern, string Replacement) scrubber = (new Regex(@"\?param=[^ ]+"), "?param=...,\n");
        settings.AddRegexScrubber(scrubber);

        await VerifySpans(spansFiltered, settings, fileNameOverride: filename);
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
        var filename = "Iast.HeaderInjection.AspNetCore5." + (notVulnerable ? "NotVuln" : testCase) +
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
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, 1, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web || x.Type == SpanTypes.IastVulnerability).ToImmutableList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifySpans(spansFiltered, settings, fileNameOverride: filename);
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

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, "SQL_INJECTION", filename, expectVulnerability: true, since: since);
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
#pragma warning disable SA1311 // Static readonly fields should begin with upper-case letter
    protected static readonly (Regex RegexPattern, string Replacement) aspNetCorePathScrubber = (new Regex("\"path\": \"AspNetCore[^\\.]+\\."), "\"path\": \"AspNetCore.");
    protected static readonly (Regex RegexPattern, string Replacement) hashScrubber = (new Regex("\"hash\": .+,"), "\"hash\": XXX,");
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

        // Per-fixture path for the IAST vulnerability JSONL report. The sample app
        // process started by the fixture writes vulnerabilities here as it detects
        // them; tests can read this file instead of inspecting agent payloads.
        VulnerabilityLogPath = Path.Combine(Path.GetTempPath(), $"iast-vulns-{Guid.NewGuid():N}.jsonl");

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
        try
        {
            if (File.Exists(VulnerabilityLogPath))
            {
                File.Delete(VulnerabilityLogPath);
            }
        }
        catch
        {
            // best effort cleanup
        }
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
        await fixture.TryStartApp(this, enableSecurity: false, agentConfiguration: agentConfiguration);
        SetHttpPort(fixture.HttpPort);
    }

    protected static Task<IReadOnlyList<JObject>> ReadVulnerabilityRecordsAsync(string path, string type, int expectedMinimumCount, DateTime? since = null, int timeoutMs = 5_000)
        => ReadVulnerabilityRecordsAsync(path, new[] { type }, expectedMinimumCount, since, timeoutMs);

    protected static async Task<IReadOnlyList<JObject>> ReadVulnerabilityRecordsAsync(string path, string[] types, int expectedMinimumCount, DateTime? since = null, int timeoutMs = 5_000)
    {
        // The reporter flushes synchronously per detection, but the request handler
        // returns to us as soon as the response goes out — give the file a brief
        // window to settle before asserting.
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var matches = TryReadRecords(path, types, since);
            if (matches.Count >= expectedMinimumCount)
            {
                return matches;
            }

            await Task.Delay(50);
        }

        return TryReadRecords(path, types, since);
    }

    protected static Task VerifyVulnerabilityRecordsAsync(string path, string type, string fileName, bool expectVulnerability, DateTime? since = null, bool includeStack = false, Action<JObject> recordSanitizer = null, int timeoutMs = 5_000)
        => VerifyVulnerabilityRecordsAsync(path, new[] { type }, fileName, expectVulnerability, since, includeStack, recordSanitizer, timeoutMs);

    protected static async Task VerifyVulnerabilityRecordsAsync(string path, string[] types, string fileName, bool expectVulnerability, DateTime? since = null, bool includeStack = false, Action<JObject> recordSanitizer = null, int timeoutMs = 5_000)
    {
        var records = await ReadVulnerabilityRecordsAsync(path, types, expectedMinimumCount: expectVulnerability ? 1 : 0, since, timeoutMs);

        // Stable order across runs: snapshots compare line-by-line, so we sort
        // multi-type results by type then hash.
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

    protected async Task TestWeakHashing(string filename, MockTracerAgent agent)
    {
        var url = "/Iast/WeakHashing";
        var spans = await SendRequestsAsync(agent, new string[] { url });

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifySpans(spans, settings, fileNameOverride: filename);
    }

    private static JObject SanitizeForVerification(JObject record, bool includeStack)
    {
        // Drop volatile fields so the snapshot stays stable across runs and code edits.
        record.Remove("timestamp");

        if (record["location"] is JObject location)
        {
            ReplaceIfPresent(location, "line", "XXX");

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
                    ReplaceIfPresent(frame, "line", "XXX");
                    ReplaceIfPresent(frame, "column", "XXX");
                }
            }
        }

        return record;

        static void ReplaceIfPresent(JObject obj, string property, string placeholder)
        {
            if (obj[property] != null)
            {
                obj[property] = placeholder;
            }
        }
    }

    private static List<JObject> TryReadRecords(string path, string[] types, DateTime? since)
    {
        var matches = new List<JObject>();
        if (!File.Exists(path))
        {
            return matches;
        }

        var typeSet = new HashSet<string>(types, StringComparer.Ordinal);

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

            var recordType = record["type"]?.Value<string>();
            if (recordType is null || !typeSet.Contains(recordType))
            {
                continue;
            }

            if (since is { } cutoff)
            {
                // Skip records emitted before the test invocation — keeps each
                // test's snapshot independent of earlier tests in the same fixture.
                var ts = record["timestamp"]?.Value<string>();
                if (ts is null || !DateTime.TryParse(ts, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var emitted) || emitted < cutoff)
                {
                    continue;
                }
            }

            matches.Add(record);
        }

        return matches;
    }
}

#endif
