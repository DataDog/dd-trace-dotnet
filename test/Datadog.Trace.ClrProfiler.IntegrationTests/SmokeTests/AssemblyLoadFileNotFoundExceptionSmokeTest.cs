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

        [TargetFrameworkVersionsFact("net452;net461;netcoreapp2.1;netcoreapp3.0")]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            CheckForSmoke();
        }
    }
}
