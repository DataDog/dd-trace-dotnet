#if NET461
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class SandboxManualTracingSmokeTest : SmokeTestBase
    {
        public SandboxManualTracingSmokeTest(ITestOutputHelper output)
            : base(output, "Sandbox.ManualTracing")
        {
        }

        [Fact]
        public void NoExceptions()
        {
            CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
#endif
