// <copyright file="AspNetMvc5AsmRulesToggle.cs" company="Datadog">
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
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rcm.Models.Asm;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
using Action = Datadog.Trace.AppSec.Rcm.Models.Asm.Action;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.Security.IntegrationTests.Rcm;

[Collection("IisTests")]
public class AspNetMvc5AsmRulesToggleIntegratedWithSecurity : AspNetMvc5AsmRulesToggle
{
    public AspNetMvc5AsmRulesToggleIntegratedWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: false, enableSecurity: true)
    {
    }
}

[Collection("IisTests")]
public class AspNetMvc5AsmRulesToggleIntegratedWithoutSecurity : AspNetMvc5AsmRulesToggle
{
    public AspNetMvc5AsmRulesToggleIntegratedWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: false, enableSecurity: false)
    {
    }
}

[Collection("IisTests")]
public class AspNetMvc5AsmRulesToggleClassicWithSecurity : AspNetMvc5AsmRulesToggle
{
    public AspNetMvc5AsmRulesToggleClassicWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: true, enableSecurity: true)
    {
    }
}

[Collection("IisTests")]
public class AspNetMvc5AsmRulesToggleClassicWithoutSecurity : AspNetMvc5AsmRulesToggle
{
    public AspNetMvc5AsmRulesToggleClassicWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: true, enableSecurity: false)
    {
    }
}

public abstract class AspNetMvc5AsmRulesToggle : RcmBaseFramework, IClassFixture<IisFixture>
{
    private readonly IisFixture _iisFixture;
    private readonly string _testName;

    public AspNetMvc5AsmRulesToggle(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableSecurity)
        : base("AspNetMvc5", output, "/home/shutdown", @"test\test-applications\security\aspnet")
    {
        SetSecurity(enableSecurity);

        _iisFixture = iisFixture;
        _iisFixture.TryStartIis(this, classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
        _testName = "Security." + nameof(AspNetMvc5AsmRulesToggle)
                                + (classicMode ? ".Classic" : ".Integrated")
                                + ".enableSecurity=" + enableSecurity;
        SetHttpPort(iisFixture.HttpPort);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("LoadFromGAC", "True")]
    [Trait("RunOnWindows", "True")]
    public async Task TestGlobalRulesToggling()
    {
        const string asmProduct = "ASM";
        var url = $"/health";
        var agent = _iisFixture.Agent;
        var settings = VerifyHelper.GetSpanVerifierSettings();
        var fileId = nameof(TestGlobalRulesToggling) + Guid.NewGuid();
        var spans1 = await SendRequestsAsync(agent, url, null, 1, 1, null, userAgent: "Arachni/v");

        await agent.SetupRcmAndWait(Output, new[] { ((object)new Payload { RuleOverrides = new[] { new RuleOverride { Id = null, OnMatch = new[] { "block" }, RulesTarget = JToken.Parse(@"[{'tags': {'confidence': '1'}}]") } } }, asmProduct, fileId) });
        var spans2 = await SendRequestsAsync(agent, url, null, 1, 1, null, userAgent: "Arachni/v");

        // reset
        fileId = nameof(TestGlobalRulesToggling) + Guid.NewGuid();
        await agent.SetupRcmAndWait(Output, new[] { ((object)new Payload { RuleOverrides = Array.Empty<RuleOverride>() }, asmProduct, fileId) });
        var spans3 = await SendRequestsAsync(agent, url, null, 1, 1, null, userAgent: "Arachni/v");

        var spans = new List<MockSpan>();
        spans.AddRange(spans1);
        spans.AddRange(spans2);
        spans.AddRange(spans3);
        await VerifySpans(spans.ToImmutableList(), settings);
        await agent.SetupRcmAndWait(Output, new[] { ((object)new Payload { Actions = Array.Empty<Action>() }, asmProduct, fileId) });
    }

    protected override string GetTestName() => _testName;
}
#endif
