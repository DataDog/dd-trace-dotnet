// <copyright file="AspNetCore5AsmCustomRules.cs" company="Datadog">
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
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Rcm
{
    /// <summary>
    /// Product rcm named ASM, actions object being tested cf https://docs.google.com/document/d/1a_-isT9v_LiiGshzQZtzPzCK_CxMtMIil_2fOq9Z1RE
    /// </summary>
    public class AspNetCore5AsmCustomRules : RcmBase
    {
        private const string AsmProduct = "ASM";
        private const string CustomRuleExample = """
[
    {
      "id": "test_custom_rule",
      "name": "Test custom rule",
      "tags": {
        "type": "custom_rule",
        "category": "attack_attempt"
      },
      "conditions": [
        {
          "parameters": {
            "inputs": [
              {
                "address": "server.request.query"
              },
              {
                "address": "server.request.body"
              },
              {
                "address": "server.request.path_params"
              }
            ],
            "list": [
              "customrule"
            ]
          },
          "operator": "phrase_match"
        }
      ],
      "transformers": [
        "lowercase"
      ]
    },
]
""";

        public AspNetCore5AsmCustomRules(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: true, testName: nameof(AspNetCore5AsmActionsConfiguration))
        {
            SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "0");
            SetEnvironmentVariable(Configuration.ConfigurationKeys.AppSec.Rules, DefaultRuleFile);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestCustomRules()
        {
            var url = $"/Health/?arg=customrule_trigger";
            await TryStartApp();
            var agent = Fixture.Agent;
            var settings = VerifyHelper.GetSpanVerifierSettings();

            var spans1 = await SendRequestsAsync(agent, url);
            await agent.SetupRcmAndWait(Output, new[] { ((object)new Payload { CustomRules = (JArray)JToken.Parse(CustomRuleExample) }, AsmProduct, nameof(TestCustomRules)) });

            var spans2 = await SendRequestsAsync(agent, url);
            var spans = new List<MockSpan>();
            spans.AddRange(spans1);
            spans.AddRange(spans2);
            await VerifySpans(spans.ToImmutableList(), settings);
            // need to reset if the process is going to be reused
            await agent.SetupRcmAndWait(Output, new[] { ((object)new Payload { CustomRules = new JArray() }, AsmProduct, nameof(TestCustomRules)) });
        }

        protected override string GetTestName() => Prefix + nameof(AspNetCore5AsmCustomRules);
    }
}
#endif
