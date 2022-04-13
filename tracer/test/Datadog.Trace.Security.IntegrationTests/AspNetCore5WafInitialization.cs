// <copyright file="AspNetCore5WafInitialization.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore5WafInitialization : AspNetBase, IDisposable
    {
        public AspNetCore5WafInitialization(ITestOutputHelper outputHelper)
            : base("AspNetCore5", outputHelper, "/shutdown", testName: nameof(AspNetCore5WafInitialization))
        {
        }

        [SkippableTheory]
        [InlineData(true, HttpStatusCode.OK)]
        [InlineData(false, HttpStatusCode.OK)]
        [InlineData(true, HttpStatusCode.OK, "wrong-tags-name-rule-set.json")]
        [InlineData(false, HttpStatusCode.OK, "wrong-tags-name-rule-set.json")]
        [Trait("RunOnWindows", "True")]
        public async Task TestSecurityInitialization(bool enableSecurity, HttpStatusCode expectedStatusCode, string ruleset = null)
        {
            var url = "/Health/?[$slice]=value";
            var agent = await RunOnSelfHosted(enableSecurity, externalRulesFile: ruleset);
            var settings = VerifyHelper.GetSpanVerifierSettings(enableSecurity, (int)expectedStatusCode, ruleset);
            await TestAppSecRequestWithVerifyAsync(agent, url, null, 1, 1, settings, testInit: true);
        }
    }
}
#endif
