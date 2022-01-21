// <copyright file="SandboxAutomaticInstrumentationSmokeTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET461
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class SandboxAutomaticInstrumentationSmokeTest : SmokeTestBase
    {
        public SandboxAutomaticInstrumentationSmokeTest(ITestOutputHelper output)
            : base(output, "Sandbox.AutomaticInstrumentation")
        {
        }

        [Fact(Skip = "This test fails because the startup hook throws a System.Security.VerificationException")]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
#endif
