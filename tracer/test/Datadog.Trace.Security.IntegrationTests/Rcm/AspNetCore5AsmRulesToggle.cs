// <copyright file="AspNetCore5AsmRulesToggle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.Rcm.Models.Asm;
using Datadog.Trace.Configuration;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
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

            var spans0 = await SendRequestsAsync(agent, url + "&DefaultConfig");
            var fileId = nameof(TestRulesToggling);
            var payload1 = ((object)new Payload { RuleOverrides = new[] { new RuleOverride { Id = ruleId, Enabled = false } } }, ASMProduct, fileId + "1");
            var payload2 = ((object)new Payload { RuleOverrides = new[] { new RuleOverride { Id = ruleId, Enabled = true } } }, ASMProduct, fileId + "1");
            var payload3 = ((object)new Payload { RuleOverrides = new[] { new RuleOverride { Id = ruleId, Enabled = true, OnMatch = new[] { "block" } } } }, ASMProduct, fileId + "2");
            var payload4 = ((object)new Payload { RuleOverrides = new[] { new RuleOverride { Id = ruleId, Enabled = true } } }, ASMProduct, fileId + "3");

            var request1 = await agent.SetupRcmAndWait(Output, new[] { payload1 });
            CheckAckState(request1, ASMProduct, 1, ApplyStates.ACKNOWLEDGED, null, "First RCM call");
            var spans1 = await SendRequestsAsync(agent, url + "&RCMCall=1");

            var request2 = await agent.SetupRcmAndWait(Output, new[] { payload2 });
            CheckAckState(request2, ASMProduct, 1, ApplyStates.ACKNOWLEDGED, null, "Second RCM call");
            var spans2 = await SendRequestsAsync(agent, url + "&RCMCall=2");

            var request3 = await agent.SetupRcmAndWait(Output, new[] { payload2, payload3 });
            CheckAckState(request3, ASMProduct, 2, ApplyStates.ACKNOWLEDGED, null, "Third RCM call");
            var spans3 = await SendRequestsAsync(agent, url + "&RCMCall=3");

            var fileId4 = nameof(TestRulesToggling) + Guid.NewGuid();
            var request4 = await agent.SetupRcmAndWait(Output, new[] { payload4 });
            CheckAckState(request4, ASMProduct, 1, ApplyStates.ACKNOWLEDGED, null, "Forth RCM call");
            var spans4 = await SendRequestsAsync(agent, url + "&RCMCall=4");

            var spans = new List<MockSpan>();
            spans.AddRange(spans0);
            spans.AddRange(spans1);
            spans.AddRange(spans2);
            spans.AddRange(spans3);
            spans.AddRange(spans4);

            await VerifySpans(spans.ToImmutableList(), settings);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestGlobalRulesToggling()
        {
            var url = "/Health/";
            await TryStartApp();
            var agent = Fixture.Agent;
            var settings = VerifyHelper.GetSpanVerifierSettings(nameof(TestGlobalRulesToggling));
            var fileId = nameof(TestRulesToggling);

            // ruleId = "crs-942-290" should be triggered;
            var spans0 = await SendRequestsAsync(agent, url, null, 1, 1, string.Empty, userAgent: "acunetix-product");
            var payload1 = ((object)new Payload { RuleOverrides = new[] { new RuleOverride { Id = null, OnMatch = new[] { "block" }, RulesTarget = JToken.Parse(@"[{'tags': {'confidence': '1'}}]") } } }, ASMProduct, productId: fileId + "1");
            var request1 = await agent.SetupRcmAndWait(Output, new[] { payload1 });
            CheckAckState(request1, ASMProduct, 1, ApplyStates.ACKNOWLEDGED, null, "First RCM call");

            var spans1 = await SendRequestsAsync(agent, url);

            // reset
            var request2 = await agent.SetupRcmAndWait(Output, []);
            CheckAckState(request2, ASMProduct, 0, ApplyStates.ACKNOWLEDGED, null, "Reset RCM call");
            var spans2 = await SendRequestsAsync(agent, url);

            var spans = new List<MockSpan>();
            spans.AddRange(spans0);
            spans.AddRange(spans1);
            spans.AddRange(spans2);

            await VerifySpans(spans.ToImmutableList(), settings, testName: Prefix + nameof(TestGlobalRulesToggling));
        }

        protected override string GetTestName() => Prefix + nameof(AspNetCore5AsmRulesToggle);
    }
}
#endif
