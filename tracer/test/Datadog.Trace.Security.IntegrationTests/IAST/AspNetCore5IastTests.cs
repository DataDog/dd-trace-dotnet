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
    public async Task TestIastInsecureCookieRequest(string url)
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
}

public abstract class AspNetCore5IastTests : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
    public AspNetCore5IastTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableIast, string testName, bool? isIastDeduplicationEnabled = null, int? samplingRate = null, int? vulnerabilitiesPerRequest = null, bool? redactionEnabled = false)
        : base("AspNetCore5", outputHelper, "/shutdown", testName: testName)
    {
        Fixture = fixture;
        fixture.SetOutput(outputHelper);
        IastEnabled = enableIast;
        IsIastDeduplicationEnabled = isIastDeduplicationEnabled;
        VulnerabilitiesPerRequest = vulnerabilitiesPerRequest;
        SamplingRate = samplingRate;
        RedactionEnabled = redactionEnabled;
    }

    protected AspNetCoreTestFixture Fixture { get; }

    protected bool IastEnabled { get; }

    protected bool? RedactionEnabled { get; }

    protected bool? IsIastDeduplicationEnabled { get; }

    protected int? VulnerabilitiesPerRequest { get; }

    protected int? SamplingRate { get; }

    public override void Dispose()
    {
        base.Dispose();
        Fixture.SetOutput(null);
    }

    public virtual async Task TryStartApp()
    {
        EnableIast(IastEnabled);
        EnableEvidenceRedaction(RedactionEnabled);
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
