using System.IO;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class ManagedLibraryMissingSmokeTest : SmokeTestBase
    {
        public ManagedLibraryMissingSmokeTest(ITestOutputHelper output)
            : base(output, "MissingLibraryCrash", maxTestRunSeconds: 15)
        {
            var smokeTestOutputPath = EnvironmentHelper.GetSampleApplicationOutputDirectory();
            var badIntegrationsFile = Path.Combine(smokeTestOutputPath, "bad-integrations.json");
            IntegrationsFileOverride = badIntegrationsFile;
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
