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

        public AspNetCore5IastTests(ITestOutputHelper outputHelper)
            : base("AspNetCore5", outputHelper, "/shutdown", testName: nameof(AspNetCore5IastTests))
        {
        }

        [SkippableTheory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("RunOnWindows", "True")]
        public async Task TestIastWeakHashingRequest(bool enableIast)
        {
            var filename = enableIast ? "Iast.weakHashing.AspNet5.IastEnabled" : "Iast.weakHashing.AspNet5.IastDisabled";
            var url = "/Iast/WeakHashing";
            var agent = await RunOnSelfHosted(enableSecurity: false, enableIast: enableIast);
            var spans = await SendRequestsAsync(agent, new string[] { url });

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(LocationMsgRegex, string.Empty);
            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName(filename)
                              .DisableRequireUniquePrefix();
        }
    }
}
#endif
