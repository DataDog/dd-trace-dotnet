// <copyright file="SandboxManualTracingSmokeTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET461
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class SandboxManualTracingSmokeTest : SmokeTestBase
    {
        public SandboxManualTracingSmokeTest(ITestOutputHelper output)
            : base(output, "Sandbox.ManualTracing")
        {
        }

        [Fact]
        public void NoExceptions()
        {
            CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
#endif
