// <copyright file="AspNetCore5AsmActionsConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rcm.Models.Asm;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;
using Action = Datadog.Trace.AppSec.Rcm.Models.Asm.Action;

namespace Datadog.Trace.Security.IntegrationTests.Rcm;

/// <summary>
/// Product rcm named ASM, actions object being tested cf https://docs.google.com/document/d/1a_-isT9v_LiiGshzQZtzPzCK_CxMtMIil_2fOq9Z1RE
/// </summary>
public class AspNetCore5AsmActionsConfiguration : RcmBase
{
    private const string AsmProduct = "ASM";

    public AspNetCore5AsmActionsConfiguration(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, enableSecurity: true, testName: nameof(AspNetCore5AsmActionsConfiguration))
    {
        SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "0");
        SetEnvironmentVariable(Configuration.ConfigurationKeys.AppSec.Rules, DefaultRuleFile);
    }

    [SkippableTheory]
    [InlineData(BlockingAction.BlockRequestType, 200, "dummy_rule", "block")]
    [InlineData(BlockingAction.RedirectRequestType, 302, "dummy_rule", "block")]
    [InlineData(BlockingAction.BlockRequestType, 200, "dummy_custom_action", "customblock")]
    [InlineData(BlockingAction.RedirectRequestType, 302, "dummy_custom_action", "customblock")]
    [Trait("RunOnWindows", "True")]
    public async Task TestBlockingAction(string type, int statusCode, string argument, string actionName)
    {
        var url = $"/Health/?arg={argument}";
        await TryStartApp();
        var agent = Fixture.Agent;
        var settings = VerifyHelper.GetSpanVerifierSettings(type, statusCode, argument, actionName);

        // Restore the default values
        await agent.SetupRcmAndWait(Output, new[] { ((object)new Payload { Actions = new[] { new Action { Id = actionName, Type = BlockingAction.BlockRequestType, Parameters = new Parameter { StatusCode = 404, Type = "auto" } } } }, AsmProduct, nameof(TestBlockingAction)) });
        var spans1 = await SendRequestsAsync(agent, url);

        // New values
        await agent.SetupRcmAndWait(Output, new[] { ((object)new Payload { Actions = new[] { new Action { Id = actionName, Type = type, Parameters = new Parameter { StatusCode = statusCode, Type = "html", Location = "/redirect" } } } }, AsmProduct, nameof(TestBlockingAction)) });

        var spans2 = await SendRequestsAsync(agent, url);
        var spans = new List<MockSpan>();
        spans.AddRange(spans1);
        spans.AddRange(spans2);
        await VerifySpans(spans.ToImmutableList(), settings);
    }

    protected override string GetTestName() => Prefix + nameof(AspNetCore5AsmActionsConfiguration);
}
#endif
