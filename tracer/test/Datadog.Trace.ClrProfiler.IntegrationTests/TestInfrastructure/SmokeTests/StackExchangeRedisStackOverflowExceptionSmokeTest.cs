// <copyright file="StackExchangeRedisStackOverflowExceptionSmokeTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.TestCollections;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "1")]
    [Collection(nameof(StackExchangeRedisTestCollection))]
    public class StackExchangeRedisStackOverflowExceptionSmokeTest : SmokeTestBase
    {
        public StackExchangeRedisStackOverflowExceptionSmokeTest(ITestOutputHelper output, StackExchangeRedisFixture redisFixture)
            : base(output, "StackExchange.Redis.StackOverflowException", maxTestRunSeconds: 30)
        {
            foreach (var variable in redisFixture.GetEnvironmentVariables())
            {
                SetEnvironmentVariable(variable.Key, variable.Value);
            }
        }

        [SkippableFact]
        [Trait("Category", "Smoke")]
        public async Task NoExceptions()
        {
            Skip.If(EnvironmentTools.IsWindows(), "Ignored for Windows");
            await CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
