// <copyright file="AspNetMvc5Rasp.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
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
        SetSecurity(true);
        EnableIast(enableIast);
        AddCookies(new Dictionary<string, string> { { "cookie-key", "cookie-value" } });
        EnableIastTelemetry((int)IastMetricsVerbosityLevel.Off);
        EnableEvidenceRedaction(false);
        SetEnvironmentVariable("DD_IAST_DEDUPLICATION_ENABLED", "false");
        SetEnvironmentVariable("DD_IAST_REQUEST_SAMPLING", "100");
        SetEnvironmentVariable("DD_IAST_MAX_CONCURRENT_REQUESTS", "100");
        SetEnvironmentVariable("DD_IAST_VULNERABILITIES_PER_REQUEST", "100");
        DisableObfuscationQueryString();
        SetEnvironmentVariable(Configuration.ConfigurationKeys.AppSec.Rules, "rasp-rule-set.json");

        _iisFixture = iisFixture;
        _classicMode = classicMode;
        _enableIast = enableIast;

        SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
    }

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [InlineData("/Iast/GetFileContent?file=filename", "Lfi")]
    [InlineData("/Iast/GetFileContent?file=/etc/password", "Lfi")]
    [InlineData("/Iast/SsrfAttack?host=127.0.0.1", "SSRF")]
    [InlineData("/Iast/SsrfAttackNoCatch?host=127.0.0.1", "SSRF")]
    [InlineData("/Iast/ExecuteCommand?file=ls&argumentLine=;evilCommand&fromShell=true", "CmdI")]
    public async Task TestRaspRequest(string url, string exploit)
    {
        AddHeaders(new()
        {
            { "Accept-Language", "en_UK" },
            { "X-Custom-Header", "42" },
            { "AnotherHeader", "Value" },
        });

        var agent = _iisFixture.Agent;
        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.UseParameters(url, exploit);
        settings.AddIastScrubbing();
        var dateTime = DateTime.UtcNow;
        var testName = _enableIast ? "RaspIast.AspNetMvc5" : "Rasp.AspNetMvc5";
        testName += _classicMode ? ".Classic" : ".Integrated";
        await SubmitRequest(url, null, "application/json");
        var spans = agent.WaitForSpans(2, minDateTime: dateTime);
        await VerifySpans(spans, settings, testName: testName, methodNameOverride: exploit);
    }

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [InlineData("/Iast/ExecuteQueryFromBodyQueryData", "SqlI", "{\"UserName\": \"' or '1'='1\"}")]
    public async Task TestRaspRequestSqlInBody(string url, string exploit, string body = null)
    {
        var agent = _iisFixture.Agent;
        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.UseParameters(url, exploit, body);
        settings.AddIastScrubbing();
        var dateTime = DateTime.UtcNow;
        var answer = await SubmitRequest("/Iast/PopulateDDBB", null, string.Empty);
        _iisFixture.Agent.SpanFilters.Add(s => !s.Resource.Contains("/Iast/PopulateDDBB"));
        agent.WaitForSpans(2, minDateTime: dateTime);
        dateTime = DateTime.UtcNow;
        var testName = _enableIast ? "RaspIast.AspNetMvc5" : "Rasp.AspNetMvc5";
        testName += _classicMode ? ".Classic" : ".Integrated";
        await SubmitRequest(url, body, "application/json");
        var spans = agent.WaitForSpans(2, minDateTime: dateTime);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        await VerifySpans(spansFiltered.ToImmutableList(), settings, testName: testName, methodNameOverride: exploit);
    }

    public async Task InitializeAsync()
    {
        await _iisFixture.TryStartIis(this, _classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
        SetHttpPort(_iisFixture.HttpPort);
        // we need to have a first request to the home page to avoid the initialization metrics of the waf and sampling priority set to 2.0 because of that
        var answer = await SubmitRequest("/", null, string.Empty);
        // because of this we need to add a filter
        _iisFixture.Agent.SpanFilters.Add(s => !s.Resource.Contains("home/index"));
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
#endif
