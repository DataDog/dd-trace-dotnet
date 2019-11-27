using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class MultiThreadedSmokeTest : SmokeTestBase
    {
        public MultiThreadedSmokeTest(ITestOutputHelper output)
            : base(output, "DataDogThreadTest")
        {
        }

        [Fact(Skip = "Investigation needed to understand timeout issues.")]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
