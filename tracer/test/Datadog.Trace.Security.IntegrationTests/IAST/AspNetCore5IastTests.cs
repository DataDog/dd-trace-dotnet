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
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Iast.Telemetry;
using Datadog.Trace.Security.IntegrationTests.IAST;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast;

// Use this class to test common vulnerabilities
public class AspNetCore5IastTestsFullSamplingIastEnabled : AspNetCore5IastTestsFullSampling
{
    public AspNetCore5IastTestsFullSamplingIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, enableIast: true, vulnerabilitiesPerRequest: 200, isIastDeduplicationEnabled: false, testName: "AspNetCore5IastTestsFullSamplingIastEnabled")
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
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spans, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
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
        await VerifyHelper.VerifySpans(spans, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastXpathInjectionRequest()
    {
        var filename = "Iast.XpathInjection.AspNetCore5.IastEnabled";
        var url = "/Iast/XpathInjection?user=klaus&value=pass";
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
    public async Task TestIastReflectionInjectionRequest()
    {
        var filename = "Iast.ReflectionInjection.AspNetCore5.IastEnabled";
        const string type = "System.String";
        var url = $"/Iast/TypeReflectionInjection?type={type}";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, [url]);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestNewtonsoftJsonParseTainting()
    {
        var filename = "Iast.NewtonsoftJsonParseTainting.AspNetCore5.IastEnabled";
        var url = "/Iast/NewtonsoftJsonParseTainting?json={\"key\": \"value\"}";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, [url]);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

#if !NETFRAMEWORK && NETCOREAPP3_1_OR_GREATER
    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestSystemTextJsonParseTainting()
    {
        var filename = "Iast.SystemTextJsonParseTainting.AspNetCore5.IastEnabled";
        var url = "/Iast/SystemTextJsonParseTainting?json={\"key\": \"value\"}";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, [url]);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
    }
#endif

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastEmailHtmlInjectionRequest()
    {
        var filename = "Iast.EmailHtmlInjection.AspNetCore5.IastEnabled";
        var url = $"/Iast/SendEmail?email=alice@aliceland.com&name=Alice&lastname=Stevens&escape=false";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, [url]);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastStoredXssRequest()
    {
        var filename = "Iast.StoredXss.AspNetCore5." + (IastEnabled ? "IastEnabled" : "IastDisabled");
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/StoredXss?param=<b>RawValue</b>";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, 2, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web || x.Type == SpanTypes.IastVulnerability).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        settings.AddRegexScrubber(aspNetCorePathScrubber);
        settings.AddRegexScrubber(hashScrubber);
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastStoredXssEscapedRequest()
    {
        var filename = "Iast.StoredXssEscaped.AspNetCore5." + (IastEnabled ? "IastEnabled" : "IastDisabled");
        var url = "/Iast/StoredXssEscaped";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, 2, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web || x.Type == SpanTypes.IastVulnerability).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();

        // Add a scrubber to remove the "?param=<value>" from the a single line
        (Regex RegexPattern, string Replacement) scrubber = (new Regex(@"\?param=[^ ]+"), "?param=...,\n");
        settings.AddRegexScrubber(scrubber);

        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastStoredSqliRequest()
    {
        var filename = "Iast.StoredSqli.AspNetCore5." + (IastEnabled ? "IastEnabled" : "IastDisabled");
        var url = "/Iast/StoredSqli";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, 2, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web || x.Type == SpanTypes.IastVulnerability).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        settings.AddRegexScrubber(aspNetCorePathScrubber);
        settings.AddRegexScrubber(hashScrubber);
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
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
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
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

        var filename = "Iast.MaxRanges.AspNetCore5.IastEnabled." + maxRangesConfiguration + "." + nbrRangesCreated;
        var url = "/Iast/MaxRanges?count=" + nbrRangesCreated + "&tainted=taintedString|";

        IncludeAllHttpSpans = true;

        // Using a new fixture here to use a new process that applies
        // correctly the new environment variable value that is changing between tests
        var newFixture = new AspNetCoreTestFixture();
        newFixture.SetOutput(Output);
        await TryStartApp(newFixture);

        var agent = newFixture.Agent;
        var spans = await SendRequestsAsync(agent, [url]);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();

        newFixture.Dispose();
        newFixture.SetOutput(null);
    }
}

// Class to test particular features (not running all the default tests)
public class AspNetCore5IastTestsStackTraces : AspNetCore5IastTests
{
    public AspNetCore5IastTestsStackTraces(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, enableIast: true, testName: "AspNetCore5IastTestsStackTraces", samplingRate: 100, isIastDeduplicationEnabled: false, vulnerabilitiesPerRequest: 200, redactionEnabled: true)
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

                foreach (var key in span.MetaStruct.Keys.ToArray())
                {
                    span.MetaStruct[key] = [];
                }
            }
        }

        await VerifyHelper.VerifySpans(spans, settings)
                          .UseFileName(fileName)
                          .DisableRequireUniquePrefix();
    }
}

public abstract class AspNetCore5IastTests50PctSamplingIastEnabled : AspNetCore5IastTests
{
    public AspNetCore5IastTests50PctSamplingIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, enableIast: true, testName: "AspNetCore5IastTests50PctSamplingIastEnabled", isIastDeduplicationEnabled: false, vulnerabilitiesPerRequest: 100, samplingRate: 50)
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

    [Fact]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    public async Task TestStackTraceLeak()
    {
        var filename = "Iast.StackTraceLeak.AspNetCore5.NotVulnerable";
        var url = "/Iast/StackTraceLeak";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, [url]);

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spans, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastJsonTagSizeExceeded()
    {
        var filename = "Iast.JsonTagSizeExceeded.AspNetCore5.TelemetryEnabled";
        var url = "/Iast/TestJsonTagSizeExceeded?tainted=taint";
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
        : base(fixture, outputHelper, enableIast: true, testName: "AspNetCore5IastTestsVariableVulnerabilityPerRequestIastEnabled", isIastDeduplicationEnabled: false, samplingRate: 100, vulnerabilitiesPerRequest: vulnerabilitiesPerRequest)
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

public class AspNetCore5IastTestsRestartedSampleIastEnabled : AspNetCore5IastTests
{
    public AspNetCore5IastTestsRestartedSampleIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, enableIast: true, vulnerabilitiesPerRequest: 200, isIastDeduplicationEnabled: false, testName: "AspNetCore5IastTestsRestartedSampleIastEnabled", redactionEnabled: true, samplingRate: 100)
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

        var filename = "Iast.DirectoryListingLeak.AspNetCore5.IastEnabled";
        var newFixture = new AspNetCoreTestFixture();
        newFixture.SetOutput(Output);

        var datetimeOffset = DateTimeOffset.UtcNow; // Catch vulnerability at the startup of the app
        await TryStartApp(newFixture);

        var agent = newFixture.Agent;
        var spans = agent.WaitForSpans(1, minDateTime: datetimeOffset);

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spans, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();

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

        var filename = "Iast.SessionIdleTimeout.AspNetCore5.IastEnabled";
        var newFixture = new AspNetCoreTestFixture();
        newFixture.SetOutput(Output);

        var datetimeOffset = DateTimeOffset.UtcNow; // Catch vulnerability at the startup of the app
        await TryStartApp(newFixture);

        var agent = newFixture.Agent;
        var spans = agent.WaitForSpans(1, minDateTime: datetimeOffset);

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

        await VerifyHelper.VerifySpans(spans, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();

        newFixture.Dispose();
        newFixture.SetOutput(null);
    }
}

public class AspNetCore5IastTestsStandaloneBillingIastEnabled : AspNetCore5IastTests
{
    public AspNetCore5IastTestsStandaloneBillingIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, enableIast: true, vulnerabilitiesPerRequest: 200, isIastDeduplicationEnabled: false, testName: "AspNetCore5IastTestsStandaloneBillingIastEnabled", redactionEnabled: true, samplingRate: 100)
    {
        // Set environment variable to enable the Standalone ASM Billing feature
        SetEnvironmentVariable("DD_EXPERIMENTAL_APPSEC_STANDALONE_ENABLED", "true");
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestApmDisabledAndAppsecIastReporting()
    {
        var filename = "Iast.StandaloneBilling.AspNetCore5.IastEnabled";

        // Testing a Reflection Injection vulnerability
        var url = $"/Iast/TypeReflectionInjection?type=System.String";
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

[Collection(nameof(AspNetCore5IastTestsFullSampling))]
[CollectionDefinition(nameof(AspNetCore5IastTestsFullSampling), DisableParallelization = true)]
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
    public async Task TestIastNoSqlMongoDbInjectionRequest()
    {
        var filename = IastEnabled ? "Iast.NoSqlMongoDbInjection.AspNetCore5.IastEnabled" : "Iast.NoSqlMongoDbInjection.AspNetCore5.IastDisabled";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        const string value = "1\", \"$or\": [{\"Price\": {\"$gt\": 1000}}], \"other\": \"1";
        var url = $"/Iast/NoSqlQueryMongoDb?price={value}";
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
        var filename = $"Iast.AspNetCore5.enableIast={IastEnabled}.path ={sanitisedUrl}";
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

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [SkippableTheory]
    [InlineData("BasicAuth", "Authorization", "Basic QWxhZGRpbjpvcGVuIHNlc2FtZQ==")]
    [InlineData("BasicAuth", "Authorization", "basic QWxhZGRpbjpvcGVuIHNlc2FtZQ==")]
    [InlineData("BasicAuth", "Authorization", "    bAsic    QWxhZGRpbjpvcGVuIHNlc2FtZQ==")]
    [InlineData("DigestAuth", "Authorization", "digest realm=\"testrealm@host.com\", qop=\"auth,auth-int\", nonce=\"dcd98b7102dd2f0e8b11d0f600bfb0c093\", opaque=\"5ccc069c403ebaf9f0171e9517f40e41\"")]
    public async Task TestIastInsecureAuthProtocolRequest(string name, string header, string data)
    {
        var filename = IastEnabled ? "Iast.InsecureAuthProtocol.AspNetCore5.IastEnabled." + name : "Iast.InsecureAuthProtocol.AspNetCore5.IastDisabled";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }

        var url = "/Iast/InsecureAuthProtocol";
        IncludeAllHttpSpans = true;
        AddHeaders(new Dictionary<string, string> { { header, data } });
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, [url]);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
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

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastReflectedXssRequest()
    {
        var filename = "Iast.ReflectedXss.AspNetCore5." + (IastEnabled ? "IastEnabled" : "IastDisabled");
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/ReflectedXss?param=<b>RawValue</b>";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, 2, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web || x.Type == SpanTypes.IastVulnerability).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        settings.AddRegexScrubber(aspNetCorePathScrubber);
        settings.AddRegexScrubber(hashScrubber);
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
    }

    [SkippableTheory]
    [InlineData("RawValue")]
    [InlineData("<script>alert('XSS')</script>")]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastReflectedXssEscapedRequest(string param)
    {
        var filename = "Iast.ReflectedXssEscaped.AspNetCore5." + (IastEnabled ? "IastEnabled" : "IastDisabled");
        var url = "/Iast/ReflectedXssEscaped?param=" + param;
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, 2, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web || x.Type == SpanTypes.IastVulnerability).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();

        // Add a scrubber to remove the "?param=<value>"
        (Regex RegexPattern, string Replacement) scrubber = (new Regex(@"\?param=[^ ]+"), "?param=...,\n");
        settings.AddRegexScrubber(scrubber);

        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
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
        var notVulnerable = testCase.StartsWith("notvulnerable", StringComparison.OrdinalIgnoreCase) || !IastEnabled;
        var filename = "Iast.HeaderInjection.AspNetCore5." + (notVulnerable ? "NotVuln" : testCase) +
            (useValueFromOriginHeader ? ".origin" : string.Empty);
        if (!notVulnerable && RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        if (!IastEnabled) { filename += ".IastDisabled"; }
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
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web || x.Type == SpanTypes.IastVulnerability).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastCustomSpanRequestAttribute()
    {
        var filename = "Iast.CustomAttribute.AspNetCore5." + (IastEnabled ? "IastEnabled" : "IastDisabled");
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/CustomAttribute?userName=Vicent";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, 3, new string[] { url });
        var spansFiltered = spans.Where(s => !s.Resource.StartsWith("CREATE TABLE") && !s.Resource.StartsWith("INSERT INTO")).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        settings.AddRegexScrubber(new Regex(@"_dd.agent_psr: .{1,3},"), string.Empty);
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastCustomSpanRequestManual()
    {
        var filename = "Iast.CustomManual.AspNetCore5." + (IastEnabled ? "IastEnabled" : "IastDisabled");
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/CustomManual?userName=Vicent";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, 3, new string[] { url });
        var spansFiltered = spans.Where(s => !s.Resource.StartsWith("CREATE TABLE") && !s.Resource.StartsWith("INSERT INTO")).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestNHibernateSqlInjection()
    {
        var filename = "Iast.NHibernateSqlInjection.AspNetCore5." + (IastEnabled ? "IastEnabled" : "IastDisabled");
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/NHibernateQuery?username=TestUser";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, [url]);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }
}

public abstract class AspNetCore5IastTests : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
#pragma warning disable SA1311 // Static readonly fields should begin with upper-case letter
    protected static readonly (Regex RegexPattern, string Replacement) aspNetCorePathScrubber = (new Regex("\"path\": \"AspNetCore[^\\.]+\\."), "\"path\": \"AspNetCore.");
    protected static readonly (Regex RegexPattern, string Replacement) hashScrubber = (new Regex("\"hash\": .+,"), "\"hash\": XXX,");
#pragma warning restore SA1311 // Static readonly fields should begin with upper-case letter

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

        SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        SetEnvironmentVariable(ConfigurationKeys.AppSec.StackTraceEnabled, "false");
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
        await TryStartApp(Fixture);
    }

    public virtual async Task TryStartApp(AspNetCoreTestFixture fixture)
    {
        EnableIast(IastEnabled);
        EnableEvidenceRedaction(RedactionEnabled);
        EnableIastTelemetry(IastTelemetryLevel);
        DisableObfuscationQueryString();
        SetEnvironmentVariable(ConfigurationKeys.Iast.IsIastDeduplicationEnabled, IsIastDeduplicationEnabled?.ToString() ?? string.Empty);
        SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilitiesPerRequest, VulnerabilitiesPerRequest?.ToString() ?? string.Empty);
        SetEnvironmentVariable(ConfigurationKeys.Iast.RequestSampling, SamplingRate?.ToString() ?? string.Empty);
        await fixture.TryStartApp(this, enableSecurity: false);
        SetHttpPort(fixture.HttpPort);
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
