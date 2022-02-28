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

        [SkippableFact]
        [Trait("Category", "Smoke")]
        public void Fails()
        {
            CheckForSmoke(shouldDeserializeTraces: false, expectedExitCode: -10);
        }
    }
}
#endif
