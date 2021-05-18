// <copyright file="StackExchangeRedisAssemblyConflictSdkProjectSmokeTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Core.Tools;
using Datadog.Trace.ClrProfiler.IntegrationTests.TestCollections;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    [Collection(nameof(StackExchangeRedisTestCollection))]
    public class StackExchangeRedisAssemblyConflictSdkProjectSmokeTest : SmokeTestBase
    {
        public StackExchangeRedisAssemblyConflictSdkProjectSmokeTest(ITestOutputHelper output)
            : base(output, "StackExchange.Redis.AssemblyConflict.SdkProject", maxTestRunSeconds: 30)
        {
        }

        [Fact]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            if (EnvironmentTools.IsWindows())
            {
                Output.WriteLine("Ignored for Windows");
                return;
            }

            CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
