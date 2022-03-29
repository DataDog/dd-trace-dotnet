// <copyright file="StackExchangeRedisAssemblyConflictLegacyProjectSmokeTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.IntegrationTests.TestCollections;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    [Collection(nameof(StackExchangeRedisTestCollection))]
    public class StackExchangeRedisAssemblyConflictLegacyProjectSmokeTest : SmokeTestBase
    {
        public StackExchangeRedisAssemblyConflictLegacyProjectSmokeTest(ITestOutputHelper output)
            : base(output, "StackExchange.Redis.AssemblyConflict.LegacyProject", maxTestRunSeconds: 30)
        {
        }

        [Fact(Skip = ".NET Framework test, but cannot run on Windows because it requires Redis")]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
