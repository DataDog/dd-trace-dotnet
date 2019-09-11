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

        [TargetFrameworkVersionsFact("netcoreapp2.1")]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            CheckForSmoke();
        }
    }
}
