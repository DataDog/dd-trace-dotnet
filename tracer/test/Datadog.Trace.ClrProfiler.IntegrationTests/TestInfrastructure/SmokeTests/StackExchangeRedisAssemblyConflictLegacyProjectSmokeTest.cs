// <copyright file="StackExchangeRedisAssemblyConflictLegacyProjectSmokeTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.TestCollections;
using Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    [Trait("RequiresDockerDependency", "true")]
    [Collection(nameof(StackExchangeRedisTestCollection))]
    public class StackExchangeRedisAssemblyConflictLegacyProjectSmokeTest : SmokeTestBase
    {
        public StackExchangeRedisAssemblyConflictLegacyProjectSmokeTest(ITestOutputHelper output, StackExchangeRedisFixture redisFixture)
            : base(output, "StackExchange.Redis.AssemblyConflict.LegacyProject", maxTestRunSeconds: 30)
        {
            foreach (var variable in redisFixture.GetEnvironmentVariables())
            {
                SetEnvironmentVariable(variable.Key, variable.Value);
            }
        }

        [Fact]
        [Trait("Category", "Smoke")]
        [Trait("SkipInCI", "True")] // .NET Framework test, but cannot run on Windows in CI because it requires Redis
        public async Task NoExceptions()
        {
            await CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
