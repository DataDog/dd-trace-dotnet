// <copyright file="AspNetCore5ExclusionFilters.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore5ExclusionFilters : AspNetBase, IDisposable
    {
        public AspNetCore5ExclusionFilters(ITestOutputHelper outputHelper)
            : base("AspNetCore5", outputHelper, "/shutdown", testName: nameof(AspNetCore5ExclusionFilters))
        {
        }

        [SkippableTheory]
        [InlineData("allow-ip-url-combo")]
        [Trait("RunOnWindows", "True")]
        public async Task TestAllowIp(string test)
        {
            var url = "/admin/?[$slice]=value";
            var agent = await RunOnSelfHosted(true, externalRulesFile: "rules-with-exclusion-filters.json");

            var settings = VerifyHelper.GetSpanVerifierSettings(test);
            await TestAppSecRequestWithVerifyAsync(agent, url, null, 1, 1, settings, ip: "192.0.240.56");
        }

        [SkippableTheory]
        [InlineData("allow-user-agent", "/Health/?arg&[$slice]")]
        [InlineData("allow-user-agent", "/health/params/appscan_fingerprint")]
        [Trait("RunOnWindows", "True")]
        public async Task TestAllowUserAgent(string test, string url)
        {
            var agent = await RunOnSelfHosted(true, externalRulesFile: "rules-with-exclusion-filters.json");

            var settings = VerifyHelper.GetSpanVerifierSettings(test, url);
            await TestAppSecRequestWithVerifyAsync(agent, url, null, 1, 1, settings, userAgent: "MyAllowedScanner");
        }
    }
}
#endif
