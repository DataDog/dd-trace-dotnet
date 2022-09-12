// <copyright file="AspNetCore5AsmToggle.cs" company="Datadog">
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
    public class AspNetCore5AsmToggle : AspNetBase, IDisposable
    {
        public AspNetCore5AsmToggle(ITestOutputHelper outputHelper)
            : base("AspNetCore5", outputHelper, "/shutdown", testName: nameof(AspNetCore5AsmToggle))
        {
        }

        [SkippableTheory]
        [InlineData(true)]
        [InlineData(false)]
        [InlineData(null)]
        [Trait("RunOnWindows", "True")]
        public async Task TestSecurityToggling(bool? enableSecurity)
        {
            SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "500");
            var url = "/Health/?[$slice]=value";
            var agent = await RunOnSelfHosted(enableSecurity);
            var settings = VerifyHelper.GetSpanVerifierSettings(enableSecurity);
            var testStart = DateTime.UtcNow;

            var spans1 = await SendRequestsAsync(agent, url);

            agent.SetupRcm(Output, new[] { ((object)new Features() { Asm = new Asm() { Enabled = false } }, "1") }, "FEATURES");
            await Task.Delay(1000);

            var spans2 = await SendRequestsAsync(agent, url);

            agent.SetupRcm(Output, new[] { ((object)new Features() { Asm = new Asm() { Enabled = true } }, "2") }, "FEATURES");
            await Task.Delay(1000);

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
