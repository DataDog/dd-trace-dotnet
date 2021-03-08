#if !NET452
using Datadog.Core.Tools;
using Datadog.Trace.ClrProfiler.IntegrationTests.TestCollections;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    [Collection(nameof(StackExchangeRedisTestCollection))]
    public class StackExchangeRedisStackOverflowExceptionSmokeTest : SmokeTestBase
    {
        public StackExchangeRedisStackOverflowExceptionSmokeTest(ITestOutputHelper output)
            : base(output, "StackExchange.Redis.StackOverflowException", maxTestRunSeconds: 30)
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
#endif
