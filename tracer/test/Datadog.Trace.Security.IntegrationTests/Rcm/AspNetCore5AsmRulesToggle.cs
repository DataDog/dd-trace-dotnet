// <copyright file="AspNetCore5AsmRulesToggle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.RcmModels.Asm;
using Datadog.Trace.Configuration;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Rcm
{
    public class AspNetCore5AsmRulesToggle : RcmBase
    {
        private const string ASMProduct = "ASM";

        public AspNetCore5AsmRulesToggle(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: true, testName: nameof(AspNetCore5AsmRulesToggle))
        {
            SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "0");
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestRulesToggling()
        {
            var url = "/Health/?[$slice]=value";
            await TryStartApp();
            var agent = Fixture.Agent;
            var settings = VerifyHelper.GetSpanVerifierSettings();
            var ruleId = "crs-942-290";

            var spans0 = await SendRequestsAsync(agent, url);
            var acknowledgedId = nameof(TestRulesToggling);

            var request1 = await agent.SetupRcmAndWait(Output, new[] { ((object)new Payload { RuleOverrides = new[] { new RuleOverride { Id = ruleId, Enabled = false } } }, acknowledgedId) }, ASMProduct, appliedServiceNames: new[] { acknowledgedId });
            CheckAckState(request1, ASMProduct, ApplyStates.ACKNOWLEDGED, null, "First RCM call");

            var spans1 = await SendRequestsAsync(agent, url);

            var acknowledgedId2 = nameof(TestRulesToggling) + 2;
            var request2 = await agent.SetupRcmAndWait(Output, new[] { ((object)new Payload { RuleOverrides = new[] { new RuleOverride { Id = ruleId, Enabled = true } } }, acknowledgedId2) }, ASMProduct, appliedServiceNames: new[] { acknowledgedId2 });
            CheckAckState(request2, ASMProduct, ApplyStates.ACKNOWLEDGED, null, "Second RCM call");
            var spans2 = await SendRequestsAsync(agent, url);

            var acknowledgedId3 = nameof(TestRulesToggling) + 3;
            var request3 = await agent.SetupRcmAndWait(Output, new[] { ((object)new Payload { RuleOverrides = new[] { new RuleOverride { Id = ruleId, Enabled = true, OnMatch = new[] { "block" } } } }, acknowledgedId3) }, ASMProduct, appliedServiceNames: new[] { acknowledgedId3 });
            CheckAckState(request3, ASMProduct, ApplyStates.ACKNOWLEDGED, null, "Third RCM call");
            var spans3 = await SendRequestsAsync(agent, url);

            var spans = new List<MockSpan>();
            spans.AddRange(spans0);
            spans.AddRange(spans1);
            spans.AddRange(spans2);
            spans.AddRange(spans3);

            await VerifySpans(spans.ToImmutableList(), settings);
        }

        protected override string GetTestName() => Prefix + nameof(AspNetCore5AsmRulesToggle);
    }
}
#endif
