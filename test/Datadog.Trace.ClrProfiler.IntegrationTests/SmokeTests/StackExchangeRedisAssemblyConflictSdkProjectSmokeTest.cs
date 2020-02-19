using Datadog.Core.Tools;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class StackExchangeRedisAssemblyConflictSdkProjectSmokeTest : SmokeTestBase
    {
        public StackExchangeRedisAssemblyConflictSdkProjectSmokeTest(ITestOutputHelper output)
            : base(output, "StackExchange.Redis.AssemblyConflict.SdkProject", maxTestRunSeconds: 15)
        {
        }

        [TargetFrameworkVersionsFact("net452;net461;netcoreapp2.1;netcoreapp3.1")]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
