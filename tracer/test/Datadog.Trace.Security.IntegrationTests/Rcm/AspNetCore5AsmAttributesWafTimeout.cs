// <copyright file="AspNetCore5AsmAttributesWafTimeout.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.RcmModels.Asm;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Rcm
{
    /// <summary>
    /// Product rcm named ASM, actions object being tested cf https://docs.google.com/document/d/1a_-isT9v_LiiGshzQZtzPzCK_CxMtMIil_2fOq9Z1RE
    /// </summary>
    public class AspNetCore5AsmAttributesWafTimeout : RcmBase
    {
        private const string AsmProduct = "ASM";

        public AspNetCore5AsmAttributesWafTimeout(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: true, testName: nameof(AspNetCore5AsmAttributesWafTimeout))
        {
            SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "1");
        }

        [SkippableTheory]
        [InlineData("/", 200)]
        [Trait("RunOnWindows", "True")]
        public async Task TestWafTimeout(string type, int statusCode)
        {
            var url = $"/health";
            await TryStartApp();
            var agent = Fixture.Agent;
            var settings = VerifyHelper.GetSpanVerifierSettings(type, statusCode);
            var acknowledgedId = nameof(TestWafTimeout) + Guid.NewGuid();

            var spans1 = await SendRequestsAsync(agent, url);
            acknowledgedId = nameof(TestWafTimeout) + Guid.NewGuid();

            var rcmWafData = new Payload
            {
                Data = new Data[]
                {
                    new()
                    {
                        Attributes = new Attributes
                        {
                            CustomAttributes = new Dictionary<string, object>
                            {
                                { "waf_timeout", 1 },
                            }
                        },
                        Id = "3dd-0uc-h1s"
                    }
                }
            };

            await agent.SetupRcmAndWait(Output, new[] { ((object)rcmWafData, acknowledgedId) }, AsmProduct, appliedServiceNames: new[] { acknowledgedId });

            var spans2 = await SendRequestsAsync(agent, url);
            var spans = new List<MockSpan>();
            spans.AddRange(spans1);
            spans.AddRange(spans2);
            await VerifySpans(spans.ToImmutableList(), settings);
        }

        protected override string GetTestName() => Prefix + nameof(AspNetCore5AsmAttributesWafTimeout);
    }
}
#endif
