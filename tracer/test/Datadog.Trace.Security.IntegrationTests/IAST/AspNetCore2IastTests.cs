// <copyright file="AspNetCore2IastTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP2_1

using System;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast
{
    public class AspNetCore2IastTests : AspNetBase, IDisposable
    {
        private static readonly Regex LocationMsgRegex = new(@"(\S)*""location"": {(\r|\n){1,2}(.*(\r|\n){1,2}){0,3}(\s)*},");
        private static readonly Regex ClientIp = new(@"["" ""]*http.client_ip: .*,(\r|\n){1,2}");
        private static readonly Regex NetworkClientIp = new(@"["" ""]*network.client.ip: .*,(\r|\n){1,2}");

        public AspNetCore2IastTests(ITestOutputHelper outputHelper)
            : base("AspNetCore2", outputHelper, "/shutdown", testName: nameof(AspNetCore2IastTests))
        {
        }

        [SkippableTheory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("RunOnWindows", "True")]
        public async Task TestIastNotWeakRequest(bool enableIast)
        {
            var filename = enableIast ? "Iast.NotWeak.AspNetCore2.IastEnabled" : "Iast.NotWeak.AspNetCore2.IastDisabled";
            var url = "/Iast";
            EnableIast(enableIast);
            IncludeAllHttpSpans = true;
            var agent = await RunOnSelfHosted(enableSecurity: false);
            var spans = await SendRequestsAsync(agent, new string[] { url });

            var settings = VerifyHelper.GetSpanVerifierSettings();
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
            var filename = enableIast ? "Iast.WeakHashing.AspNetCore2.IastEnabled" : "Iast.WeakHashing.AspNetCore2.IastDisabled";
            EnableIast(enableIast);
            IncludeAllHttpSpans = true;
            var agent = await RunOnSelfHosted(enableSecurity: false);
            await TestWeakHashing(filename, agent);
        }

        [SkippableTheory]
        [InlineData(1)]
        [InlineData(2)]
        [Trait("RunOnWindows", "True")]
        public async Task TestIastWeakHashingRequestVulnerabilitiesPerRequest(int vulnerabilitiesPerRequest)
        {
            SetEnvironmentVariable(ConfigurationKeys.Iast.IsIastDeduplicationEnabled, "true");
            SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilitiesPerRequest, vulnerabilitiesPerRequest.ToString());
            SetEnvironmentVariable(ConfigurationKeys.Iast.RequestSampling, "100");
            EnableIast(true);
            IncludeAllHttpSpans = true;
            var filename = vulnerabilitiesPerRequest == 1 ? "Iast.WeakHashing.AspNetCore2.IastEnabled.SingleVulnerability" : "Iast.WeakHashing.AspNetCore2.IastEnabled";
            var agent = await RunOnSelfHosted(enableSecurity: false);
            await TestWeakHashing(filename, agent);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestIastWeakHashingRequestSampling()
        {
            SetEnvironmentVariable(ConfigurationKeys.Iast.IsIastDeduplicationEnabled, "false");
            SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilitiesPerRequest, "100");
            SetEnvironmentVariable(ConfigurationKeys.Iast.RequestSampling, "50");
            EnableIast(true);
            IncludeAllHttpSpans = true;
            var filename = "Iast.WeakHashing.AspNetCore2.IastEnabled";
            var agent = await RunOnSelfHosted(enableSecurity: false);
            await TestWeakHashing(filename, agent);

            filename = "Iast.WeakHashing.AspNetCore2.IastDisabled";
            await TestWeakHashing(filename, agent);

            filename = "Iast.WeakHashing.AspNetCore2.IastEnabled";
            await TestWeakHashing(filename, agent);
        }

        private async Task TestWeakHashing(string filename, MockTracerAgent agent)
        {
            var url = "/Iast/WeakHashing";
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
