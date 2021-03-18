using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class DuplicateTypeProxySmokeTest : SmokeTestBase
    {
        public DuplicateTypeProxySmokeTest(ITestOutputHelper output)
            : base(output, "DuplicateTypeProxy")
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
