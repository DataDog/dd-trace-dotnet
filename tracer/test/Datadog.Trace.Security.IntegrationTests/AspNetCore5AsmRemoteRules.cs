// <copyright file="AspNetCore5AsmRemoteRules.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore5AsmRemoteRules : AspNetBase
    {
        public AspNetCore5AsmRemoteRules(ITestOutputHelper outputHelper)
            : base("AspNetCore5", outputHelper, "/shutdown", testName: nameof(AspNetCore5AsmRemoteRules))
        {
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestNewRemoteRules()
        {
            SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "500");
            var url = "/Health/?[$slice]=value";
            var agent = await RunOnSelfHosted(true);
            var settings = VerifyHelper.GetSpanVerifierSettings();
            var testStart = DateTime.UtcNow;

            var spans1 = await SendRequestsAsync(agent, url);

            await agent.SetupRcmAndWait(Output, new[] { ((object)GetRules("2.22.222"), "1") }, "ASM_DD");
            var spans2 = await SendRequestsAsync(agent, url);

            await agent.SetupRcmAndWait(Output, new[] { ((object)GetRules("3.33.333"), "2") }, "ASM_DD");
            var spans3 = await SendRequestsAsync(agent, url);

            var spans = new List<MockSpan>();
            spans.AddRange(spans1);
            spans.AddRange(spans2);
            spans.AddRange(spans3);

            await VerifySpans(spans.ToImmutableList(), settings, true);
        }

        private string GetRules(string version)
        {
            return File.ReadAllText("remote-rules.json").Replace("{VERSION}", version);
        }
    }
}
#endif
