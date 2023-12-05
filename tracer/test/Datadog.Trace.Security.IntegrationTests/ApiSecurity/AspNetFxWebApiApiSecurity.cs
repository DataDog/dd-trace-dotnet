// <copyright file="AspNetFxWebApiApiSecurity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.Rcm.Models.Asm;
using Datadog.Trace.AppSec.Rcm.Models.AsmData;
using Datadog.Trace.Configuration;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.Security.IntegrationTests.ApiSecurity;

[Collection("IisTests")]
public class AspNetFxWebApiApiSecurityEnabled : AspNetFxWebApiApiSecurity
{
    public AspNetFxWebApiApiSecurityEnabled(IisFixture fixture, ITestOutputHelper output)
        : base(fixture, output, enableApiSecurity: true)
    {
    }
}

[Collection("IisTests")]
public class AspNetFxWebApiApiSecurityDisabled : AspNetFxWebApiApiSecurity
{
    public AspNetFxWebApiApiSecurityDisabled(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, enableApiSecurity: false)
    {
    }
}

public abstract class AspNetFxWebApiApiSecurity : AspNetBase, IClassFixture<IisFixture>
{
    private readonly IisFixture _fixture;
    private readonly string _testName;

    internal AspNetFxWebApiApiSecurity(IisFixture fixture, ITestOutputHelper output, bool enableApiSecurity)
        : base("WebApi", output, "/home/shutdown", @"test\test-applications\security\aspnet")
    {
        SetSecurity(true);
        if (enableApiSecurity)
        {
            EnvironmentHelper.CustomEnvironmentVariables.Add(ConfigurationKeys.AppSec.ApiExperimentalSecurityEnabled, "true");
            EnvironmentHelper.CustomEnvironmentVariables.Add(ConfigurationKeys.AppSec.ApiSecurityRequestSampleRate, "1");
        }

        _fixture = fixture;
        AddCookies(new Dictionary<string, string> { { "cookie-key", "cookie-value" } });
        _fixture.TryStartIis(this, IisAppType.AspNetIntegrated);
        _testName = "Security." + nameof(AspNetFxWebApiApiSecurity)
                                + ".enableApiSecurity=" + enableApiSecurity;
        SetHttpPort(fixture.HttpPort);
    }

    [SkippableTheory]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    [Trait("LoadFromGAC", "True")]
    [InlineData("/api/home/api-security/12", """{"Dog1":"23", "Dog2":"test", "Dog3": 2.5, "Dog4": 1.6}""", HttpStatusCode.OK, false)]
    [InlineData("/api/home/api-security/12", """{"Dog1":"23", "Dog2":"dev/zero", "Dog3": 2.5, "Dog4": 1.6}""", HttpStatusCode.Forbidden, true)]
    [InlineData("/api/home/empty-model", """{"Dog1":"23", "Dog2":"test", "Dog3": 1.5, "Dog4": 1.6}""", HttpStatusCode.OK, false)]
    public async Task TestApiSecurityScan(string url, string body, HttpStatusCode expectedStatusCode, bool containsAttack)
    {
        var agent = _fixture.Agent;
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedUrl, body.Substring(0, 10), expectedStatusCode, containsAttack);
        var fileId = nameof(AspNetFxWebApiApiSecurity) + Guid.NewGuid();
        await agent.SetupRcmAndWait(Output, new[] { ((object)new AppSec.Rcm.Models.Asm.Payload { RuleOverrides = [new RuleOverride { Id = "crs-932-160", Enabled = true, OnMatch = ["block"] }] }, "ASM", fileId) });

        var dateTime = DateTime.UtcNow;

        var result = await SubmitRequest(url, body, "application/json");
        var spans = agent.WaitForSpans(2, minDateTime: dateTime);
        await VerifySpans(spans, settings);
    }

    protected override string GetTestName() => _testName;
}
#endif
