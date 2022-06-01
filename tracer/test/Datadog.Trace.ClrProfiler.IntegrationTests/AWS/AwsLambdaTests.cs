// <copyright file="AwsLambdaTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AWS
{
    [UsesVerify]
    [Trait("RequiresDockerDependency", "true")]
    public class AwsLambdaTests : TestHelper
    {
        public AwsLambdaTests(ITestOutputHelper output)
            : base("AWS.Lambda", output)
        {
        }

        [SkippableFact]
        [Trait("Category", "ArmUnsupported")]
        [Trait("Category", "Lambda")]
        public async Task SubmitsTraces()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IsAlpine")))
            {
                Output.WriteLine("Skipping");
                return;
            }

            using var agent = EnvironmentHelper.GetMockAgent(fixedPort: 5002);
            using (RunSampleAndWaitForExit(agent))
            {
                var spans = agent.WaitForSpans(12, 15_000).ToArray();

                var settings = VerifyHelper.GetSpanVerifierSettings();
                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName(nameof(AwsLambdaTests));
            }
        }
    }
}
#endif
