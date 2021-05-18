// <copyright file="TraceContextSmokeTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class TraceContextSmokeTest : SmokeTestBase
    {
        public TraceContextSmokeTest(ITestOutputHelper output)
            : base(output, "TraceContext.InvalidOperationException", maxTestRunSeconds: 120 * 10)
        {
        }

        [Fact(Skip ="Skipping until this test is refactored into a different assembly. The load may be interrupting the rest of the test suite.")]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            if (!EnvironmentHelper.IsCoreClr())
            {
                Output.WriteLine("Ignored for .NET Framework");
                return;
            }

            CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
