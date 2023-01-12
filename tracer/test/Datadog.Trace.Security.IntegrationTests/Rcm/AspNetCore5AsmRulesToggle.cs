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
            SetHttpPort(Fixture.HttpPort);
            var settings = VerifyHelper.GetSpanVerifierSettings();
            var ruleId = "crs-942-290";

            var spans1 = await SendRequestsAsync(Fixture.Agent, url);

            var request1 = await Fixture.Agent.SetupRcmAndWait(Output, new[] { ((object)new Payload() { RuleStatus = new[] { new RuleStatus { Id = ruleId, Enabled = false } } }, "1") }, ASMProduct);
            CheckAckState(request1, ASMProduct, ApplyStates.ACKNOWLEDGED, null, "First RCM call");

            var spans2 = await SendRequestsAsync(Fixture.Agent, url);

            var request2 = await Fixture.Agent.SetupRcmAndWait(Output, new[] { ((object)new Payload() { RuleStatus = new[] { new RuleStatus { Id = ruleId, Enabled = true } } }, "2") }, ASMProduct);
            CheckAckState(request2, ASMProduct, ApplyStates.ACKNOWLEDGED, null, "Second RCM call");

            var spans3 = await SendRequestsAsync(Fixture.Agent, url);

            var spans = new List<MockSpan>();
            spans.AddRange(spans1);
            spans.AddRange(spans2);
            spans.AddRange(spans3);

            await VerifySpans(spans.ToImmutableList(), settings);
        }
    }
}
#endif
