// <copyright file="AspNetCore5AsmToggle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
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
        [Trait("RunOnWindows", "True")]
        public async Task TestSecurityToggling(bool enableSecurity)
        {
            var url = "/Health/?[$slice]=value";
            var agent = await RunOnSelfHosted(enableSecurity);
            var settings = VerifyHelper.GetSpanVerifierSettings(enableSecurity);
            var testStart = DateTime.UtcNow;

            var spans = await SendRequestsAsync(agent, url, "/DisableASM", url, "/EnableASM", url);
            await VerifySpans(spans, settings, true);
        }

        private string ToString(MockSpan span)
        {
            var sb = new StringBuilder();
            sb.AppendLine(span.ToString());
            foreach (var tuple in span.Tags)
            {
                sb.AppendLine($"  {tuple.Key} = {tuple.Value}");
            }

            return sb.ToString();
        }
    }
}
#endif
