#if !NET452
using Datadog.Core.Tools;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
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
