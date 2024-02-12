// <copyright file="AspNetMvc5Rasp.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.Iast.Telemetry;
using Datadog.Trace.Security.IntegrationTests.IAST;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Rasp;

[Collection("IisTests")]
public class AspMvc5RaspEnabledIastDisabledClassic : AspNetMvc5RaspTests
{
    public AspMvc5RaspEnabledIastDisabledClassic(IisFixture fixture, ITestOutputHelper outputHelper)
    : base(fixture, outputHelper, classicMode: true, enableIast: false)
    {
    }
}

[Collection("IisTests")]
public class AspMvc5RaspEnabledIastEnabledClassic : AspNetMvc5RaspTests
{
    public AspMvc5RaspEnabledIastEnabledClassic(IisFixture fixture, ITestOutputHelper outputHelper)
    : base(fixture, outputHelper, classicMode: true, enableIast: true)
    {
    }
}

[Collection("IisTests")]
public class AspMvc5RaspEnabledIastDisabledIntegrated : AspNetMvc5RaspTests
{
    public AspMvc5RaspEnabledIastDisabledIntegrated(IisFixture fixture, ITestOutputHelper outputHelper)
    : base(fixture, outputHelper, classicMode: false, enableIast: false)
    {
    }
}

[Collection("IisTests")]
public class AspMvc5RaspEnabledIastEnabledIntegrated : AspNetMvc5RaspTests
{
    public AspMvc5RaspEnabledIastEnabledIntegrated(IisFixture fixture, ITestOutputHelper outputHelper)
    : base(fixture, outputHelper, classicMode: false, enableIast: true)
    {
    }
}

public abstract class AspNetMvc5RaspTests : AspNetBase, IClassFixture<IisFixture>, IAsyncLifetime
{
    private readonly IisFixture _iisFixture;
    private readonly bool _enableIast;
    private readonly bool _classicMode;

    public AspNetMvc5RaspTests(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableIast)
        : base(nameof(AspNetMvc5), output, "/home/shutdown", @"test\test-applications\security\aspnet")
    {
        EnableRasp();
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
        _classicMode = classicMode;
        _enableIast = enableIast;

        SetSecurity(true);
        SetEnvironmentVariable(Configuration.ConfigurationKeys.AppSec.Rules, DefaultRuleFile);
        SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [Fact]
    public async Task TestRaspIastPathTraversalRequest()
    {
        var filePath = "file.csv";
        var filename = _enableIast ? "Rasp.PathTraversal.AspNetMvc5.IastEnabled" : "Rasp.PathTraversal.AspNetMvc5.IastDisabled";
        var url = $"/Iast/GetFileContent?file={filePath}";
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(AddressesConstants.RequestQuery, sanitisedUrl, null);
        var spans = await SendRequestsAsync(_iisFixture.Agent, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    public async Task InitializeAsync()
    {
        await _iisFixture.TryStartIis(this, _classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
        SetHttpPort(_iisFixture.HttpPort);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
#endif
