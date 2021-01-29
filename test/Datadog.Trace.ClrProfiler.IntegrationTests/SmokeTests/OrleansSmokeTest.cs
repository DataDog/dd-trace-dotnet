#if !NET452
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class OrleansSmokeTest : SmokeTestBase
    {
        public OrleansSmokeTest(ITestOutputHelper output)
            : base(output, "OrleansCrash")
        {
            AssumeSuccessOnTimeout = true;
        }

        [Fact]
        [Trait("Category", "Smoke")]
        [Trait("Category", "ArmUnsupported")]
        public void NoExceptions()
        {
            CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
#endif
