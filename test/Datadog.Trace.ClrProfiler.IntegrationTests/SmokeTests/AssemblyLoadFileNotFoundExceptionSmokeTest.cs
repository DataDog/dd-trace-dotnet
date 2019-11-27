using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class AssemblyLoadFileNotFoundExceptionSmokeTest : SmokeTestBase
    {
        public AssemblyLoadFileNotFoundExceptionSmokeTest(ITestOutputHelper output)
            : base(output, "AssemblyLoad.FileNotFoundException")
        {
        }

        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            CheckForSmoke();
        }
    }
}
