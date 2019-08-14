using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class TraceContextSmokeTest : SmokeTestBase
    {
        public TraceContextSmokeTest(ITestOutputHelper output)
            : base(output, "TraceContext.InvalidOperationException", maxTestRunSeconds: 60 * 10)
        {
        }

        [Fact]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            if (!EnvironmentHelper.IsCoreClr())
            {
                Output.WriteLine("Ignored for .NET Framework");
                return;
            }

            CheckForSmoke(shouldSerializeTraces: false);
        }
    }
}
