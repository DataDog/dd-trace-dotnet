// <copyright file="AspNetMvc5ApiSecurity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.Rcm.Models.Asm;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.Security.IntegrationTests.ApiSecurity;

[Collection("IisTests")]
public class AspNetMvc5ApiSecurityEnabled : AspNetMvc5ApiSecurity
{
    public AspNetMvc5ApiSecurityEnabled(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, enableApiSecurity: true)
    {
    }
}

[Collection("IisTests")]
public class AspNetMvc5ApiSecurityDisabled : AspNetMvc5ApiSecurity
{
    public AspNetMvc5ApiSecurityDisabled(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, enableApiSecurity: false)
    {
    }
}

public abstract class AspNetMvc5ApiSecurity : AspNetBase, IClassFixture<IisFixture>
{
    private readonly IisFixture _fixture;
    private readonly string _testName;

    internal AspNetMvc5ApiSecurity(IisFixture iisFixture, ITestOutputHelper output, bool enableApiSecurity)
        : base("AspNetMvc5", output, "/home/shutdown", @"test\test-applications\security\aspnet")
    {
        SetSecurity(true);
        if (enableApiSecurity)
        {
            EnvironmentHelper.CustomEnvironmentVariables.Add(ConfigurationKeys.AppSec.ApiExperimentalSecurityEnabled, "true");
            EnvironmentHelper.CustomEnvironmentVariables.Add(ConfigurationKeys.AppSec.ApiSecurityRequestSampleRate, "1");
        }

        AddCookies(new Dictionary<string, string> { { "cookie-key", "cookie-value" } });
        _fixture = iisFixture;
        _fixture.TryStartIis(this, IisAppType.AspNetIntegrated);
        _testName = "Security." + nameof(AspNetMvc5ApiSecurity)
                                + ".enableApiSecurity=" + enableApiSecurity;
        SetHttpPort(iisFixture.HttpPort);
    }

    [SkippableTheory]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    [Trait("LoadFromGAC", "True")]
    [InlineData("/home/apisecurity/12", """{"Dog1":"23", "Dog2":"test", "Dog3": 2.5, "Dog4": 1.6}""", HttpStatusCode.OK, false)]
    [InlineData("/home/apisecurity/12", """{"Dog1":"23", "Dog2":"dev/zero", "Dog3": 2.5, "Dog4": 1.6}""", HttpStatusCode.Forbidden, true)]
    [InlineData("/home/emptymodel", """{"Dog1":"23", "Dog2":"test", "Dog3": 2.5, "Dog4": 1.6}""", HttpStatusCode.OK, false)]

    public async Task TestApiSecurityScan(string url, string body, HttpStatusCode expectedStatusCode, bool containsAttack)
    {
        var agent = _fixture.Agent;
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedUrl, body.Substring(0, 10), expectedStatusCode, containsAttack);
        var fileId = nameof(AspNetMvc5ApiSecurity) + Guid.NewGuid();
        await agent.SetupRcmAndWait(Output, new[] { ((object)new Payload { RuleOverrides = [new RuleOverride { Id = "crs-932-160", Enabled = true, OnMatch = ["block"] }] }, "ASM", fileId) });
        var dateTime = DateTime.UtcNow;
        var result = await SubmitRequest(url, body, "application/json");
        var spans = agent.WaitForSpans(2, minDateTime: dateTime);
        await VerifySpans(spans, settings);
    }

    protected override string GetTestName() => _testName;
}
#endif
