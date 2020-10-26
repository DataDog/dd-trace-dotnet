#if !NETFRAMEWORK
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class NetCoreAssemblyLoadFailureOlderNuGetSmokeTest : SmokeTestBase
    {
        public NetCoreAssemblyLoadFailureOlderNuGetSmokeTest(ITestOutputHelper output)
            : base(output, "NetCoreAssemblyLoadFailureOlderNuGet")
        {
        }

        [Fact]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
#endif
