// <copyright file="AspNetMvc5AsmBlockingActions.cs" company="Datadog">
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
using Datadog.Trace.AppSec.RcmModels.Asm;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.Security.IntegrationTests.Rcm;

[Collection("IisTests")]
public class AspNetMvc5AsmBlockingActionsIntegratedWithSecurity : AspNetMvc5AsmBlockingActions
{
    public AspNetMvc5AsmBlockingActionsIntegratedWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: false, enableSecurity: true)
    {
    }
}

[Collection("IisTests")]
public class AspNetMvc5AsmBlockingActionsIntegratedWithoutSecurity : AspNetMvc5AsmBlockingActions
{
    public AspNetMvc5AsmBlockingActionsIntegratedWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: false, enableSecurity: false)
    {
    }
}

[Collection("IisTests")]
public class AspNetMvc5AsmBlockingActionsClassicWithSecurity : AspNetMvc5AsmBlockingActions
{
    public AspNetMvc5AsmBlockingActionsClassicWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: true, enableSecurity: true)
    {
    }
}

[Collection("IisTests")]
public class AspNetMvc5AsmBlockingActionsClassicWithoutSecurity : AspNetMvc5AsmBlockingActions
{
    public AspNetMvc5AsmBlockingActionsClassicWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: true, enableSecurity: false)
    {
    }
}

public abstract class AspNetMvc5AsmBlockingActions : RcmBaseFramework, IClassFixture<IisFixture>
{
    private readonly IisFixture _iisFixture;
    private readonly string _testName;

    public AspNetMvc5AsmBlockingActions(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableSecurity)
        : base("AspNetMvc5", output, "/home/shutdown", @"test\test-applications\security\aspnet")
    {
        SetSecurity(enableSecurity);
        SetEnvironmentVariable(ConfigurationKeys.AppSec.Rules, DefaultRuleFile);

        _iisFixture = iisFixture;
        _iisFixture.TryStartIis(this, classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
        _testName = "Security." + nameof(AspNetMvc5AsmBlockingActions)
                                + (classicMode ? ".Classic" : ".Integrated")
                                + ".enableSecurity=" + enableSecurity;
        SetHttpPort(iisFixture.HttpPort);
    }

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("LoadFromGAC", "True")]
    [InlineData("block_request", 200)]
    [InlineData("redirect_request", 302)]
    [Trait("RunOnWindows", "True")]
    public async Task TestBlockingAction(string type, int statusCode)
    {
        const string asmProduct = "ASM";
        var url = $"/health?arg=dummy_rule";
        var agent = _iisFixture.Agent;
        var settings = VerifyHelper.GetSpanVerifierSettings(type, statusCode);
        var acknowledgedId = nameof(TestBlockingAction) + Guid.NewGuid();
        // need to reset if the process is going to be reused
        await agent.SetupRcmAndWait(Output, new[] { ((object)new Payload { Actions = Array.Empty<AppSec.RcmModels.Asm.Action>() }, acknowledgedId) }, asmProduct, appliedServiceNames: new[] { acknowledgedId });
        var spans1 = await SendRequestsAsync(agent, url);
        acknowledgedId = nameof(TestBlockingAction) + Guid.NewGuid();
        await agent.SetupRcmAndWait(Output, new[] { ((object)new Payload { Actions = new[] { new AppSec.RcmModels.Asm.Action { Id = "block", Type = type, Parameters = new Parameter { StatusCode = statusCode, Type = "html", Location = "/redirect" } } } }, acknowledgedId) }, asmProduct, appliedServiceNames: new[] { acknowledgedId });

        var spans2 = await SendRequestsAsync(agent, url);
        var spans = new List<MockSpan>();
        spans.AddRange(spans1);
        spans.AddRange(spans2);
        await VerifySpans(spans.ToImmutableList(), settings);
        await agent.SetupRcmAndWait(Output, new[] { ((object)new Payload { Actions = Array.Empty<AppSec.RcmModels.Asm.Action>() }, acknowledgedId) }, asmProduct, appliedServiceNames: new[] { acknowledgedId });
    }

    protected override string GetTestName() => _testName;
}
#endif
