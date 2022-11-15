// <copyright file="AspNetCore5IastTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast
{
    public class AspNetCore5IastTests : AspNetBase, IDisposable
    {
        private static readonly Regex LocationMsgRegex = new(@"(\S)*""location"": {(\r|\n){1,2}(.*(\r|\n){1,2}){0,3}(\s)*},");
        private static readonly Regex ClientIp = new(@"["" ""]*http.client_ip: .*,(\r|\n){1,2}");
        private static readonly Regex NetworkClientIp = new(@"["" ""]*network.client.ip: .*,(\r|\n){1,2}");

        public AspNetCore5IastTests(ITestOutputHelper outputHelper)
            : base("AspNetCore5", outputHelper, "/shutdown", testName: nameof(AspNetCore5IastTests))
        {
        }

        [SkippableTheory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("RunOnWindows", "True")]
        public async Task TestIastNotWeakRequest(bool enableIast)
        {
            var filename = enableIast ? "Iast.NotWeak.AspNetCore5.IastEnabled" : "Iast.NotWeak.AspNetCore5.IastDisabled";
            var url = "/Iast";
            EnableIast(enableIast);
            IncludeAllHttpSpans = true;
            var agent = await RunOnSelfHosted(enableSecurity: false);
            var spans = await SendRequestsAsync(agent, new string[] { url });

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(ClientIp, string.Empty);
            settings.AddRegexScrubber(NetworkClientIp, string.Empty);
            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName(filename)
                              .DisableRequireUniquePrefix();
        }

        [SkippableTheory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("RunOnWindows", "True")]
        public async Task TestIastWeakHashingRequest(bool enableIast)
        {
            var filename = enableIast ? "Iast.WeakHashing.AspNetCore5.IastEnabled" : "Iast.WeakHashing.AspNetCore5.IastDisabled";
            var url = "/Iast/WeakHashing";
            EnableIast(enableIast);
            IncludeAllHttpSpans = true;
            var agent = await RunOnSelfHosted(enableSecurity: false);
            var spans = await SendRequestsAsync(agent, new string[] { url });

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(LocationMsgRegex, string.Empty);
            settings.AddRegexScrubber(ClientIp, string.Empty);
            settings.AddRegexScrubber(NetworkClientIp, string.Empty);
            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName(filename)
                              .DisableRequireUniquePrefix();
        }
    }
}
#endif
