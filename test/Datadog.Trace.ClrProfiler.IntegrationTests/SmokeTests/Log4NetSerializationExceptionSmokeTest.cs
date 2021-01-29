#if NETFRAMEWORK
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class Log4NetSerializationExceptionSmokeTest : SmokeTestBase
    {
        public Log4NetSerializationExceptionSmokeTest(ITestOutputHelper output)
            : base(output, "Log4Net.SerializationException", maxTestRunSeconds: 120)
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
