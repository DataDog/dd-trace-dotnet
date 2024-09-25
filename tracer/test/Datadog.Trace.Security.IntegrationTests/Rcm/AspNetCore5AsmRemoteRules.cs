// <copyright file="AspNetCore5AsmRemoteRules.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.Rcm.Models.Asm;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Rcm
{
    public class AspNetCore5AsmRemoteRules : RcmBase
    {
        public AspNetCore5AsmRemoteRules(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: true, testName: nameof(AspNetCore5AsmRemoteRules))
        {
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestRemoteRules()
        {
            var url = "/Health/?[$slice]=value";
            await TryStartApp();
            var agent = Fixture.Agent;
            var settings = VerifyHelper.GetSpanVerifierSettings();

            // Test new rules
            var spans1 = await SendRequestsAsync(agent, url);
            var productId = nameof(TestRemoteRules);
            await agent.SetupRcmAndWait(Output, new List<(object Config, string ProductName, string Id)> { (GetRules("2.22.222"), "ASM_DD", productId) });
            var spans2 = await SendRequestsAsync(agent, url);
            await agent.SetupRcmAndWait(Output, new List<(object Config, string ProductName, string Id)> { (GetRules("3.33.333"), "ASM_DD", productId) });
            var spans3 = await SendRequestsAsync(agent, url);

            // Test deletion + switch back to default rules when no RC no rules + keep updated blocking action + reset blocking actions
            ResetDefaultUserAgent();
            var block405Action = new Payload() { Actions = [new Action { Id = "block", Type = "block_request", Parameters = new Parameter { StatusCode = 405, Type = "json" } }] };
            var block405ActionProductId = "action";

            await agent.SetupRcmAndWait(Output, new List<(object Config, string ProductName, string Id)> { (block405Action, "ASM", block405ActionProductId), (GetNonBlockingRules(), "ASM_DD", productId) });
            var spans4 = await SendRequestsAsync(agent, "/", null, 1, 1, null, null, "dd-test-scanner-log-block");
            // Should trigger on the new applied rule "new-test-non-blocking"
            // Should not block and return a 200

            await agent.SetupRcmAndWait(Output, new List<(object Config, string ProductName, string Id)> { (block405Action, "ASM", block405ActionProductId) });
            var spans5 = await SendRequestsAsync(agent, "/", null, 1, 1, null, null, "dd-test-scanner-log-block");
            // Should fall back to the default rules and trigger "ua0-600-56x"
            // Should block and return a 405 (from the defined action)

            await agent.SetupRcmAndWait(Output, new List<(object Config, string ProductName, string Id)> { (new Payload { Actions = [] }, "ASM", block405ActionProductId) });
            var spans6 = await SendRequestsAsync(agent, "/", null, 1, 1, null, null, "dd-test-scanner-log-block");
            // Should use the default rules with no defined action and trigger "ua0-600-56x"
            // Should block and return a 403

            var spans = new List<MockSpan>();
            spans.AddRange(spans1);
            spans.AddRange(spans2);
            spans.AddRange(spans3);
            spans.AddRange(spans4);
            spans.AddRange(spans5);
            spans.AddRange(spans6);

            await VerifySpans(spans.ToImmutableList(), settings);
        }

        protected override string GetTestName() => Prefix + nameof(AspNetCore5AsmRemoteRules);

        private string GetRules(string version)
        {
            return File.ReadAllText("remote-rules.json").Replace("{VERSION}", version);
        }

        private string GetNonBlockingRules()
        {
            return File.ReadAllText("remote-rules-override-blocking.json");
        }
    }
}
#endif
