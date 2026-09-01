// <copyright file="SandboxLegacySecurityPolicySmokeTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    /// <summary>
    /// Tests the behavior of the tracer when instrumenting an app that has NetFx40_LegacySecurityPolicy and a custom AppDomainManager.
    /// Expected behavior: the tracer should fail gracefully without impacting the app.
    /// Wrong behavior: the app crashes or the tracer affects the security level of the appdomain.
    /// </summary>
    public class SandboxLegacySecurityPolicySmokeTest : SmokeTestBase
    {
        public SandboxLegacySecurityPolicySmokeTest(ITestOutputHelper output)
            : base(output, "Sandbox.LegacySecurityPolicy")
        {
        }

        [SkippableFact]
        [Trait("Category", "Smoke")]
        public async Task Fails()
        {
            await CheckForSmoke(shouldDeserializeTraces: false, expectedExitCode: 0);
        }
    }
}
#endif
