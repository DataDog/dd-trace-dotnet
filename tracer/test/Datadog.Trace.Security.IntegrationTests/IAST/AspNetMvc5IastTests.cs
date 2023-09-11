// <copyright file="AspNetMvc5IastTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
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
}

[Collection("IisTests")]
public class AspNetMvc5IntegratedWithoutIast : AspNetMvc5IastTests
{
    public AspNetMvc5IntegratedWithoutIast(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: false, enableIast: false)
    {
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
public class AspNetMvc5ClassicWithIastTelemetryEnabled : AspNetBase, IClassFixture<IisFixture>
{
    private readonly IisFixture _iisFixture;
    private readonly string _testName;

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

        _iisFixture = iisFixture;
        _iisFixture.TryStartIis(this, IisAppType.AspNetClassic);
        _testName = "Security." + nameof(AspNetMvc5) + ".TelemetryEnabled" +
                 ".Classic" + ".enableIast=true";
        SetHttpPort(iisFixture.HttpPort);
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
        settings.AddIastScrubbing(true);
        var sanitisedPath = VerifyHelper.SanitisePathsForVerify(url);
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName($"{_testName}.path={sanitisedPath}")
                          .DisableRequireUniquePrefix();
    }
}

[Collection("IisTests")]
public class AspNetMvc5ClassicWithoutIast : AspNetMvc5IastTests
{
    public AspNetMvc5ClassicWithoutIast(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: true, enableIast: false)
    {
    }
}

public abstract class AspNetMvc5IastTests : AspNetBase, IClassFixture<IisFixture>
{
    private readonly IisFixture _iisFixture;
    private readonly string _testName;
    private readonly bool _enableIast;

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

        _iisFixture = iisFixture;
        _enableIast = enableIast;
        _iisFixture.TryStartIis(this, classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
        _testName = "Security." + nameof(AspNetMvc5)
                 + (classicMode ? ".Classic" : ".Integrated")
                 + ".enableIast=" + enableIast;
        SetHttpPort(iisFixture.HttpPort);
    }

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
        settings.AddIastScrubbing(false);
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
        var filename = _enableIast ? "Iast.SqlInjection.AspNetMvc5.IastEnabled" : "Iast.SqlInjection.AspNetMvc5.IastDisabled";
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
        var filename = _enableIast ? "Iast.PathTraversal.AspNetMvc5.IastEnabled" : "Iast.PathTraversal.AspNetMvc5.IastDisabled";
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
        var filename = _enableIast ? "Iast.CommandInjection.AspNetMvc5.IastEnabled" : "Iast.CommandInjection.AspNetMvc5.IastDisabled";
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
        var filename = _enableIast ? "Iast.SSRF.AspNetMvc5.IastEnabled" : "Iast.SSRF.AspNetMvc5.IastDisabled";
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
        SetEnvironmentVariable("DD_TRACE_DEBUG", "1");
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(test, sanitisedUrl, body);
        var spans = await SendRequestsAsync(_iisFixture.Agent, new string[] { url });
        var filename = _enableIast ? "Iast.Ldap.AspNetMvc5.IastEnabled" : "Iast.Ldap.AspNetMvc5.IastDisabled";
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
        var filename = _enableIast ? "Iast.CookieTainting.AspNetMvc5.IastEnabled" : "Iast.CookieTainting.AspNetMvc5.IastDisabled";
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                            .UseFileName(filename)
                            .DisableRequireUniquePrefix();
    }

    protected override string GetTestName() => _testName;
}
#endif
