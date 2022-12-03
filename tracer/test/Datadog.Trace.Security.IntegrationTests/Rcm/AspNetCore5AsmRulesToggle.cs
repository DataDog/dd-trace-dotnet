// <copyright file="AspNetCore5AsmRulesToggle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.RcmModels.Asm;
using Datadog.Trace.Configuration;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Rcm
{
    public class AspNetCore5AsmRulesToggle : RcmBase
    {
        private const string ASMProduct = "ASM";

        public AspNetCore5AsmRulesToggle(ITestOutputHelper outputHelper)
            : base(outputHelper, testName: nameof(AspNetCore5AsmRulesToggle))
        {
            SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "0");
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestRulesToggling()
        {
            var url = "/Health/?[$slice]=value";
            var agent = await RunOnSelfHosted(true);
            var settings = VerifyHelper.GetSpanVerifierSettings();
            var ruleId = "crs-942-290";

            var spans1 = await SendRequestsAsync(agent, url);

            var request1 = await agent.SetupRcmAndWait(Output, new[] { ((object)new Payload() { RuleStatus = new[] { new RuleStatus { Id = ruleId, Enabled = false } } }, "1") }, ASMProduct);
            CheckAckState(request1, ASMProduct, ApplyStates.ACKNOWLEDGED, null, "First RCM call");

            var spans2 = await SendRequestsAsync(agent, url);

            var request2 = await agent.SetupRcmAndWait(Output, new[] { ((object)new Payload() { RuleStatus = new[] { new RuleStatus { Id = ruleId, Enabled = true } } }, "2") }, ASMProduct);
            CheckAckState(request2, ASMProduct, ApplyStates.ACKNOWLEDGED, null, "Second RCM call");

            var spans3 = await SendRequestsAsync(agent, url);

            var spans = new List<MockSpan>();
            spans.AddRange(spans1);
            spans.AddRange(spans2);
            spans.AddRange(spans3);

            await VerifySpans(spans.ToImmutableList(), settings, true);
        }
    }
}
#endif
