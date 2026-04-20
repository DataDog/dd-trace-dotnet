// <copyright file="AspNetWebFormsWithIast.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Telemetry;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
namespace Datadog.Trace.Security.IntegrationTests.IAST;

[Collection("IisTests")]
public class AspNetWebFormsIntegratedWithIast : AspNetWebFormsWithIast
{
    public AspNetWebFormsIntegratedWithIast(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: false, enableSecurity: true)
    {
    }
}

[Collection("IisTests")]
public class AspNetWebFormsClassicIntegratedWithIast : AspNetWebFormsWithIast
{
    public AspNetWebFormsClassicIntegratedWithIast(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: true, enableSecurity: true)
    {
    }
}

public abstract class AspNetWebFormsWithIast : AspNetBase, IClassFixture<IisFixture>, IAsyncLifetime
{
    private readonly IisFixture _iisFixture;
    private readonly bool _classicMode;

    public AspNetWebFormsWithIast(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableSecurity)
        : base("WebForms", output, "/home/shutdown", @"test\test-applications\security\aspnet")
    {
        EnableIast(true);
        EnableEvidenceRedaction(false);
        EnableIastTelemetry((int)IastMetricsVerbosityLevel.Off);
        SetEnvironmentVariable("DD_IAST_DEDUPLICATION_ENABLED", "false");
        SetEnvironmentVariable("DD_IAST_REQUEST_SAMPLING", "100");
        SetEnvironmentVariable("DD_IAST_MAX_CONCURRENT_REQUESTS", "100");
        SetEnvironmentVariable("DD_IAST_VULNERABILITIES_PER_REQUEST", "100");
        SetEnvironmentVariable(Configuration.ConfigurationKeys.AppSec.StackTraceEnabled, "false");

        _iisFixture = iisFixture;
        _classicMode = classicMode;
        _testName = "Security." + nameof(AspNetWebForms)
                 + (classicMode ? ".Classic" : ".Integrated")
                 + ".enableSecurity=" + enableSecurity;
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData("TestQueryParameterNameVulnerability")]
    public async Task TestQueryParameterNameVulnerability(string test)
    {
        var url = "/print?Encrypt=True&ClientDatabase=774E4D65564946426A53694E48756B592B444A6C43673D3D&p=413&ID=2376&EntityType=114&Print=True&OutputType=WORDOPENXML&SSRSReportID=1";

        var settings = VerifyHelper.GetSpanVerifierSettings(test);
        settings.AddIastScrubbing();

        await TestAppSecRequestWithVerifyAsync(_iisFixture.Agent, url, null, 1, 1, settings, userAgent: "Hello/V");
    }

    // XSS via Response.Write — WebForms pattern. The tracer only instruments
    // HtmlString..ctor (MVC Razor's Html.Raw), not HttpResponse.Write, so this
    // should produce NO XSS vulnerability on plain WebForms.
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableFact]
    public async Task Xss_ResponseWrite_NotDetected()
    {
        var url = "/Iast/Xss.aspx?input=scriptalert";
        var spans = await SendRequestsAsync(_iisFixture.Agent, new[] { url });
        var rootSpan = spans.FirstOrDefault(s => s.Name == "aspnet.request");

        if (rootSpan == null)
        {
            return;
        }

        if (rootSpan.MetaStruct is not null)
        {
            IastVerifyScrubberExtensions.IastMetaStructScrubbing(rootSpan);
        }

        var iastJson = rootSpan.GetTag(Tags.IastJson);
        if (iastJson != null)
        {
            var parsed = JObject.Parse(iastJson);
            var vulns = parsed["vulnerabilities"] as JArray;
            var xssVulns = vulns?.OfType<JObject>()
                .Where(v => v["type"]?.Value<string>() == "XSS")
                .ToList();
            xssVulns.Should().BeNullOrEmpty("WebForms Response.Write is not instrumented for XSS");
        }
    }

    // TrustBoundaryViolation via Session["key"] = value — WebForms pattern.
    // The tracer instruments HttpSessionStateBase (MVC abstract), not the
    // concrete HttpSessionState that WebForms uses, so this should produce
    // NO trust-boundary-violation on plain WebForms.
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableFact]
    public async Task TrustBoundaryViolation_ConcreteSession_NotDetected()
    {
        var spans = await SendRequestsAsync(_iisFixture.Agent, 1, new[] { "/Iast/TrustBoundary.aspx?name=testKey&value=testValue" });
        var rootSpan = spans.FirstOrDefault(s => s.Name == "aspnet.request");
        rootSpan.Should().NotBeNull();

        if (rootSpan.MetaStruct is not null)
        {
            IastVerifyScrubberExtensions.IastMetaStructScrubbing(rootSpan);
        }

        var iastJson = rootSpan.GetTag(Tags.IastJson);
        if (iastJson != null)
        {
            var parsed = JObject.Parse(iastJson);
            var vulns = parsed["vulnerabilities"] as JArray;
            var tbvVulns = vulns?.OfType<JObject>()
                .Where(v => v["type"]?.Value<string>() == "TRUST_BOUNDARY_VIOLATION")
                .ToList();
            tbvVulns.Should().BeNullOrEmpty("WebForms concrete HttpSessionState is not instrumented for trust boundary violation");
        }
    }

    public async Task InitializeAsync()
    {
        await _iisFixture.TryStartIis(this, _classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
        SetHttpPort(_iisFixture.HttpPort);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected override string GetTestName() => _testName;
}
#endif
