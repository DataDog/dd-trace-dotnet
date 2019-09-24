using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class MultiThreadedSmokeTest : SmokeTestBase
    {
        public MultiThreadedSmokeTest(ITestOutputHelper output)
            : base(output, "DataDogThreadTest")
        {
        }

        [TargetFrameworkVersionsFact("netcoreapp2.1;netcoreapp3.0")]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            CheckForSmoke();
        }
    }
}
