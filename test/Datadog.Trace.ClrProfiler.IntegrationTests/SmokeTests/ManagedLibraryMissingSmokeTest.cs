using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class ManagedLibraryMissingSmokeTest : SmokeTestBase
    {
        public ManagedLibraryMissingSmokeTest(ITestOutputHelper output)
            : base(output, "MissingLibraryCrash", maxTestRunSeconds: 20)
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
