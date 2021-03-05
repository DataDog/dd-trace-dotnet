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
