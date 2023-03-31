// <copyright file="AspNetCore5AsmActionsConfiguration.cs" company="Datadog">
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
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Rcm
{
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
        [InlineData("block_request", 200)]
        [InlineData("redirect_request", 302)]
        [Trait("RunOnWindows", "True")]
        public async Task TestBlockingAction(string type, int statusCode)
        {
            var url = $"/Health/?arg=dummy_rule";
            await TryStartApp();
            var agent = Fixture.Agent;
            var settings = VerifyHelper.GetSpanVerifierSettings(type, statusCode);

            var spans1 = await SendRequestsAsync(agent, url);
            await agent.SetupRcmAndWait(Output, new[] { ((object)new Payload { Actions = new[] { new Datadog.Trace.AppSec.Rcm.Models.Asm.Action { Id = "block", Type = type, Parameters = new Parameter { StatusCode = statusCode, Type = "html", Location = "/redirect" } } } }, AsmProduct, nameof(TestBlockingAction)) });

            var spans2 = await SendRequestsAsync(agent, url);
            var spans = new List<MockSpan>();
            spans.AddRange(spans1);
            spans.AddRange(spans2);
            await VerifySpans(spans.ToImmutableList(), settings);
            // need to reset if the process is going to be reused
            await agent.SetupRcmAndWait(Output, new[] { ((object)new Payload { Actions = Array.Empty<Datadog.Trace.AppSec.Rcm.Models.Asm.Action>() }, AsmProduct, nameof(TestBlockingAction)) });
        }

        protected override string GetTestName() => Prefix + nameof(AspNetCore5AsmActionsConfiguration);
    }
}
#endif
