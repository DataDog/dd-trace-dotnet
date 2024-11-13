// <copyright file="AspNetCore5Rasp.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.Rcm.Models.Asm;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Telemetry;
using Datadog.Trace.Security.IntegrationTests.IAST;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Rasp;

public class AspNetCore5RaspEnabledIastEnabled : AspNetCore5Rasp
{
    public AspNetCore5RaspEnabledIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
    : base(fixture, outputHelper, enableIast: true)
    {
    }
}

public class AspNetCore5RaspEnabledIastDisabled : AspNetCore5Rasp
{
    public AspNetCore5RaspEnabledIastDisabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
    : base(fixture, outputHelper, enableIast: false)
    {
    }

    [SkippableTheory]
    [InlineData("/Iast/GetFileContent?file=/etc/password", "Lfi")]
    [Trait("RunOnWindows", "True")]
    public async Task TestRaspRequest_ThenDisableRule_ThenEnableAgain(string url, string exploit)
    {
        var ruleId = "rasp-001-001";
        var testName = "RaspRCM.RuleEnableDisableEnable.AspNetCore5";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, [url]);

        var fileId = Guid.NewGuid().ToString();
        await agent.SetupRcmAndWait(Output, new[] { ((object)new Payload { RuleOverrides = new[] { new RuleOverride { Id = ruleId, Enabled = false } } }, "ASM", fileId) });
        spans = spans.AddRange(await SendRequestsAsync(agent, [url]));
        await agent.SetupRcmAndWait(Output, new[] { ((object)new Payload { RuleOverrides = new[] { new RuleOverride { Id = ruleId, Enabled = true } } }, "ASM", fileId) });
        spans = spans.AddRange(await SendRequestsAsync(agent, [url]));
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.UseParameters(url, exploit);
        await VerifySpans(spansFiltered.ToImmutableList(), settings, testName: testName, methodNameOverride: exploit);
    }
}

public abstract class AspNetCore5Rasp : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
    // This class is used to test RASP features either with IAST enabled or disabled. Since they both use common instrumentation
    // points, we should test that IAST works normally with or without RASP enabled.
    public AspNetCore5Rasp(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableIast)
        : base("AspNetCore5", outputHelper, "/shutdown", testName: "AspNetCore5.SecurityEnabled")
    {
        EnableRasp();
        SetSecurity(true);
        EnableIast(enableIast);
        AddCookies(new Dictionary<string, string> { { "cookie-key", "cookie-value" } });
        SetEnvironmentVariable(ConfigurationKeys.Iast.IsIastDeduplicationEnabled, "false");
        SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilitiesPerRequest, "100");
        SetEnvironmentVariable(ConfigurationKeys.Iast.RequestSampling, "100");
        SetEnvironmentVariable(ConfigurationKeys.Iast.RedactionEnabled, "true");
        SetEnvironmentVariable(ConfigurationKeys.AppSec.Rules, "rasp-rule-set.json");
        EnableEvidenceRedaction(false);
        EnableIastTelemetry((int)IastMetricsVerbosityLevel.Off);
        IastEnabled = enableIast;
        Fixture = fixture;
        Fixture.SetOutput(outputHelper);
    }

    protected bool IastEnabled { get; }

    protected AspNetCoreTestFixture Fixture { get; }

    public override void Dispose()
    {
        base.Dispose();
        Fixture.SetOutput(null);
    }

    public async Task TryStartApp()
    {
        await Fixture.TryStartApp(this, true);
        SetHttpPort(Fixture.HttpPort);
    }

    [SkippableTheory]
    [InlineData("/Iast/GetFileContent?file=/etc/password", "Lfi")]
    [InlineData("/Iast/GetFileContent?file=filename", "Lfi")]
    [InlineData("/Iast/SsrfAttack?host=127.0.0.1", "SSRF")]
    [InlineData("/Iast/ExecuteCommand?file=ls&argumentLine=;evilCommand&fromShell=true", "CmdI")]
    [Trait("RunOnWindows", "True")]
    public async Task TestRaspRequest(string url, string exploit)
    {
        AddHeaders(new()
        {
            { "Accept-Language", "en_UK" },
            { "X-Custom-Header", "42" },
            { "AnotherHeader", "Value" },
        });
        var testName = IastEnabled ? "RaspIast.AspNetCore5" : "Rasp.AspNetCore5";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, [url]);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();
        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.UseParameters(url, exploit);
        settings.AddIastScrubbing();
        await VerifySpans(spansFiltered.ToImmutableList(), settings, testName: testName, methodNameOverride: exploit);
    }

    [SkippableTheory]
    [InlineData("/Iast/ExecuteQueryFromBodyQueryData", "SqlI", "{\"UserName\": \"' or '1'='1\"}")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    public async Task TestRaspRequestSqlInBody(string url, string exploit, string body = null)
    {
        var testName = IastEnabled ? "RaspIast.AspNetCore5" : "Rasp.AspNetCore5";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        _ = await SendRequestsAsync(agent, "/Iast/PopulateDDBB", null, 1, 1, string.Empty, "application/json", null);
        var spans = await SendRequestsAsync(agent, url, body, 1, 1, string.Empty, "application/json", null);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web && !x.Resource.Contains("/Iast/PopulateDDBB")).ToList();
        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.UseParameters(url, exploit, body);
        settings.AddIastScrubbing();
        await VerifySpans(spansFiltered.ToImmutableList(), settings, testName: testName, methodNameOverride: exploit);
    }
}
#endif
