using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class OrleansSmokeTest : SmokeTestBase
    {
        public OrleansSmokeTest(ITestOutputHelper output)
            : base(output, "OrleansCrash", maxTestRunSeconds: 30)
        {
            AssumeSuccessOnTimeout = true;
        }

        [TargetFrameworkVersionsFact("net461;netcoreapp2.1;netcoreapp3.0")]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            CheckForSmoke();
        }
    }
}
