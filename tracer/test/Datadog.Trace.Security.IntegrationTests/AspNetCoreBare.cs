// <copyright file="AspNetCoreBare.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCoreBare : AspNetBase, IDisposable
    {
        public AspNetCoreBare(ITestOutputHelper outputHelper)
            : base("AspNetCoreBare", outputHelper, "/shutdown")
        {
        }

        [SkippableTheory]
        [InlineData(HttpStatusCode.OK, "/good?param=[$slice]")]
        [InlineData(HttpStatusCode.InternalServerError, "/bad?param=[$slice]")]
        [InlineData(HttpStatusCode.OK, "/void?param=[$slice]")]
        [Trait("RunOnWindows", "True")]
        public async Task TestSecurity(HttpStatusCode expectedStatusCode, string url)
        {
            using var agent = await RunOnSelfHosted(enableSecurity: true, externalRulesFile: DefaultRuleFile);

            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings((int)expectedStatusCode, sanitisedUrl);

            await TestAppSecRequestWithVerifyAsync(agent, url, null, 5, 1, settings);
        }
    }
}
#endif
