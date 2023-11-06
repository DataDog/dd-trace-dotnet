// <copyright file="AspNetCore2IastTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP2_1
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Telemetry;
using Datadog.Trace.Security.IntegrationTests.IAST;
using Datadog.Trace.TestHelpers;
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

public class AspNetCore2IastTestsSpanTelemetryIastEnabled : AspNetCore2IastTests
{
    public AspNetCore2IastTestsSpanTelemetryIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
: base(fixture, outputHelper, true, "AspNetCore2IastSpanTelemetryEnabled", iastTelemetryLevel: (int)IastMetricsVerbosityLevel.Debug, samplingRate: 100, isIastDeduplicationEnabled: false, vulnerabilitiesPerRequest: 100)
    {
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastTelemetry()
    {
        var filename = "Iast.PathTraversal.AspNetCore2.TelemetryEnabled";
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
}

public abstract class AspNetCore2IastTestsVariableVulnerabilityPerRequestIastEnabled : AspNetCore2IastTests
{
    public AspNetCore2IastTestsVariableVulnerabilityPerRequestIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, int vulnerabilitiesPerRequest)
        : base(fixture, outputHelper, enableIast: true, testName: "AspNetCore2IastTestsEnabled", isIastDeduplicationEnabled: false, samplingRate: 100, vulnerabilitiesPerRequest: vulnerabilitiesPerRequest)
    {
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastWeakHashingRequestVulnerabilitiesPerRequest()
    {
        IncludeAllHttpSpans = true;
        var filename = VulnerabilitiesPerRequest == 1 ? "Iast.WeakHashing.AspNetCore2.IastEnabled.SingleVulnerability" : "Iast.WeakHashing.AspNetCore2.IastEnabled";
        await TryStartApp();
        var agent = Fixture.Agent;
        await TestWeakHashing(filename, agent);
    }
}

public class AspNetCore2IastTestsFullSamplingEnabled : AspNetCore2IastTestsFullSampling
{
    public AspNetCore2IastTestsFullSamplingEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, enableIast: true, testName: "AspNetCore2IastTestsEnabled", isIastDeduplicationEnabled: false, vulnerabilitiesPerRequest: 200)
    {
    }

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
        var queryParams = "?contentType=" + contentType + "&returnCode=" + returnCode +
            (string.IsNullOrEmpty(xContentTypeHeaderValue) ? string.Empty : "&xContentTypeHeaderValue=" + xContentTypeHeaderValue);
        var filename = "Iast.XContentTypeHeaderMissing.AspNetCore2." + contentType.Replace("/", string.Empty) +
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
}

public class AspNetCore2IastTestsFullSamplingDisabled : AspNetCore2IastTestsFullSampling
{
    public AspNetCore2IastTestsFullSamplingDisabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, enableIast: false, testName: "AspNetCore2IastTestsDisabled")
    {
    }
}

public class AspNetCore2IastTestsFullSamplingRedactionEnabled : AspNetCore2IastTestsFullSampling
{
    public AspNetCore2IastTestsFullSamplingRedactionEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, enableIast: true, isIastDeduplicationEnabled: false, testName: "AspNetCore2IastTestsRedactionEnabled", redactionEnabled: true, vulnerabilitiesPerRequest: 200)
    {
    }
}

public abstract class AspNetCore2IastTestsFullSampling : AspNetCore2IastTests
{
    public AspNetCore2IastTestsFullSampling(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableIast, string testName, bool? isIastDeduplicationEnabled = null, int? vulnerabilitiesPerRequest = null, bool redactionEnabled = false)
        : base(fixture, outputHelper, enableIast: enableIast, testName: testName, samplingRate: 100, isIastDeduplicationEnabled: isIastDeduplicationEnabled, vulnerabilitiesPerRequest: vulnerabilitiesPerRequest, redactionEnabled: redactionEnabled)
    {
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastNotWeakRequest()
    {
        var filename = IastEnabled ? "Iast.NotWeak.AspNetCore2.IastEnabled" : "Iast.NotWeak.AspNetCore2.IastDisabled";
        var url = "/Iast";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, new string[] { url });

        var settings = VerifyHelper.GetSpanVerifierSettings();
        await VerifyHelper.VerifySpans(spans, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastWeakHashingRequest()
    {
        var filename = IastEnabled ? "Iast.WeakHashing.AspNetCore2.IastEnabled" : "Iast.WeakHashing.AspNetCore2.IastDisabled";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        await TestWeakHashing(filename, agent);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestRequestBodyTaintingRazor()
    {
        var filename = IastEnabled ? "Iast.RequestBodyTestRazor.AspNetCore2.IastEnabled" : "Iast.RequestBodyTestRazor.AspNetCore2.IastDisabled";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/DataRazorIastPage";
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
        var filename = IastEnabled ? "Iast.RequestBodyTest.AspNetCore2.IastEnabled" : "Iast.RequestBodyTest.AspNetCore2.IastDisabled";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/ExecuteQueryFromBodyQueryData";
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
        var filename = IastEnabled ? "Iast.SqlInjection.AspNetCore2.IastEnabled" : "Iast.SqlInjection.AspNetCore2.IastDisabled";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = "/Iast/SqlQuery?query=SELECT%20Surname%20from%20Persons%20where%20name%20=%20%27Vicent%27";
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
        var filename = IastEnabled ? "Iast.CommandInjection.AspNetCore2.IastEnabled" : "Iast.CommandInjection.AspNetCore2.IastDisabled";
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
        var filename = IastEnabled ? "Iast.SSRF.AspNetCore2.IastEnabled" : "Iast.SSRF.AspNetCore2.IastDisabled";
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

    [Trait("Category", "LinuxUnsupported")]
    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastLdapRequest()
    {
        var filename = IastEnabled ? "Iast.Ldap.AspNetCore2.IastEnabled" : "Iast.Ldap.AspNetCore2.IastDisabled";
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
    public async Task TestIastCookieTaintingRequest()
    {
        var filename = IastEnabled ? "Iast.CookieTainting.AspNetCore2.IastEnabled" : "Iast.CookieTainting.AspNetCore2.IastDisabled";
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
    public async Task TestIastInsecureCookieRequest(string url)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var filename = $"Security.AspNetCore2.enableIast={IastEnabled}.path ={sanitisedUrl}";
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
        var filename = IastEnabled ? "Iast.PathTraversal.AspNetCore2.IastEnabled" : "Iast.PathTraversal.AspNetCore2.IastDisabled";
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
        var filename = IastEnabled ? "Iast.WeakRandomness.AspNetCore2.IastEnabled" : "Iast.WeakRandomness.AspNetCore2.IastDisabled";
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
}

public class AspNetCore2IastTests50PctSamplingIastEnabled : AspNetCore2IastTests
{
    public AspNetCore2IastTests50PctSamplingIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, enableIast: true, testName: "AspNetCore2IastTestsEnabled", isIastDeduplicationEnabled: false, vulnerabilitiesPerRequest: 100, samplingRate: 50)
    {
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastWeakHashingRequestSampling()
    {
        IncludeAllHttpSpans = true;
        var filename = "Iast.WeakHashing.AspNetCore2.IastEnabled";
        await TryStartApp();
        var agent = Fixture.Agent;
        await TestWeakHashing(filename, agent);

        filename = "Iast.WeakHashing.AspNetCore2.IastDisabledFlag";
        await TestWeakHashing(filename, agent);

        filename = "Iast.WeakHashing.AspNetCore2.IastEnabled";
        await TestWeakHashing(filename, agent);
    }

    protected override async Task TryStartApp()
    {
        EnableIastTelemetry(IastTelemetryLevel);
        EnableIast(IastEnabled);
        EnableEvidenceRedaction(RedactionEnabled);
        DisableObfuscationQueryString();
        SetEnvironmentVariable(ConfigurationKeys.Iast.IsIastDeduplicationEnabled, IsIastDeduplicationEnabled?.ToString() ?? string.Empty);
        SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilitiesPerRequest, VulnerabilitiesPerRequest?.ToString() ?? string.Empty);
        SetEnvironmentVariable(ConfigurationKeys.Iast.RequestSampling, SamplingRate?.ToString() ?? string.Empty);
        await Fixture.TryStartApp(this, enableSecurity: false, sendHealthCheck: false);
        SetHttpPort(Fixture.HttpPort);
    }
}

public abstract class AspNetCore2IastTests : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
    public AspNetCore2IastTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableIast, string testName, bool? isIastDeduplicationEnabled = null, int? samplingRate = null, int? vulnerabilitiesPerRequest = null, bool? redactionEnabled = false, int iastTelemetryLevel = (int)IastMetricsVerbosityLevel.Off)
        : base("AspNetCore2", outputHelper, "/shutdown", testName: testName)
    {
        Fixture = fixture;
        fixture.SetOutput(outputHelper);
        IastEnabled = enableIast;
        RedactionEnabled = redactionEnabled;
        IsIastDeduplicationEnabled = isIastDeduplicationEnabled;
        VulnerabilitiesPerRequest = vulnerabilitiesPerRequest;
        SamplingRate = samplingRate;
        IastTelemetryLevel = iastTelemetryLevel;
    }

    protected AspNetCoreTestFixture Fixture { get; }

    protected bool IastEnabled { get; }

    protected bool? RedactionEnabled { get; }

    protected bool? UseTelemetry { get; }

    protected bool? IsIastDeduplicationEnabled { get; }

    protected int? VulnerabilitiesPerRequest { get; }

    protected int? SamplingRate { get; }

    protected int IastTelemetryLevel { get; }

    public override void Dispose()
    {
        base.Dispose();
        Fixture.SetOutput(null);
    }

    protected virtual async Task TryStartApp()
    {
        EnableIast(IastEnabled);
        EnableEvidenceRedaction(RedactionEnabled);
        EnableIastTelemetry(IastTelemetryLevel);
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
        var spans = await SendRequestsAsync(agent, expectedSpansPerRequest: 2, url);

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spans, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }
}
#endif
