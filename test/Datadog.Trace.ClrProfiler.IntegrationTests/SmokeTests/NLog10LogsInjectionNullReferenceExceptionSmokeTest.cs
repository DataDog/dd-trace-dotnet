#if NETFRAMEWORK
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class NLog10LogsInjectionNullReferenceExceptionSmokeTest : SmokeTestBase
    {
        public NLog10LogsInjectionNullReferenceExceptionSmokeTest(ITestOutputHelper output)
            : base(output, "NLog10LogsInjection.NullReferenceException", maxTestRunSeconds: 90)
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
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
