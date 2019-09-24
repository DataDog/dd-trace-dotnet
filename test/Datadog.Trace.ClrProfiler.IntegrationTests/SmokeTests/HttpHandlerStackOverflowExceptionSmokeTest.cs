using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class HttpHandlerStackOverflowExceptionSmokeTest : SmokeTestBase
    {
        public HttpHandlerStackOverflowExceptionSmokeTest(ITestOutputHelper output)
            : base(output, "HttpMessageHandler.StackOverflowException", maxTestRunSeconds: 15)
        {
        }

        [TargetFrameworkVersionsFact("net461;netcoreapp2.1;netcoreapp3.0")]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            CheckForSmoke();
        }
    }
}
