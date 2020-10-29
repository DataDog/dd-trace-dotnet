#if NET452
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class EntityFramework6xMdTokenLookupFailure : SmokeTestBase
    {
        public EntityFramework6xMdTokenLookupFailure(ITestOutputHelper output)
            : base(output, "EntityFramework6x.MdTokenLookupFailure", maxTestRunSeconds: 480)
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
