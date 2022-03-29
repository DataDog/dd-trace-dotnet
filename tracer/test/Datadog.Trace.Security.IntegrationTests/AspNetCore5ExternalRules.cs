// <copyright file="AspNetCore5ExternalRules.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// The conditions looks weird, but it seems like _OR_GREATER is not supported yet in all environments
// We can trim all the additional conditions when this is fixed
#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore5ExternalRules : AspNetBase, IDisposable
    {
        public AspNetCore5ExternalRules(ITestOutputHelper outputHelper)
            : base("AspNetCore5", outputHelper, "/shutdown", testName: "AspNetCore5ExternalRules")
        {
        }

        [SkippableFact]
        [Trait("Category", "ArmUnsupported")]
        public async Task TestSecurity()
        {
            var enableSecurity = true;

            // use the same rule file that's embbed but load it via an env var, to verify there's not bugs in that code path
            var externalRulesFile =
                Path.Combine(EnvironmentTools.GetSolutionDirectory(), "tracer/src/Datadog.Trace/AppSec/Waf/rule-set.json");

            var agent = await RunOnSelfHosted(enableSecurity, externalRulesFile);

            var settings = VerifyHelper.GetSpanVerifierSettings();

            await TestBlockedRequestWithVerifyAsync(agent, DefaultAttackUrl, 5, 1, settings);
        }
    }
}
#endif
