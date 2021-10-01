// <copyright file="StackExchangeRedisAssemblyConflictLegacyProjectSmokeTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using NUnit.Framework;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    [NonParallelizable]
    public class StackExchangeRedisAssemblyConflictLegacyProjectSmokeTest : SmokeTestBase
    {
        public StackExchangeRedisAssemblyConflictLegacyProjectSmokeTest()
            : base("StackExchange.Redis.AssemblyConflict.LegacyProject", maxTestRunSeconds: 30)
        {
        }

        [Test]
        [Ignore(".NET Framework test, but cannot run on Windows because it requires Redis")]
        [Property("Category", "Smoke")]
        public void NoExceptions()
        {
            CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
