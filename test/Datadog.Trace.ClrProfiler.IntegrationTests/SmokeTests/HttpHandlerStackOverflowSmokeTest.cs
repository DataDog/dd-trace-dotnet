using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class HttpHandlerStackOverflowSmokeTest : SmokeTestBase
    {
        public HttpHandlerStackOverflowSmokeTest(ITestOutputHelper output)
            : base(output, "HttpMessageHandler.StackOverflow", maxTestRunSeconds: 15)
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

            CheckForSmoke();
        }
    }
}
