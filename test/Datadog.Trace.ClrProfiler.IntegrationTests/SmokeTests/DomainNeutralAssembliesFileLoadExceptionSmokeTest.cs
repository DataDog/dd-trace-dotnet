using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class DomainNeutralAssembliesFileLoadExceptionSmokeTest : SmokeTestBase
    {
        public DomainNeutralAssembliesFileLoadExceptionSmokeTest(ITestOutputHelper output)
            : base(output, "DomainNeutralAssemblies.FileLoadException")
        {
        }

        [TargetFrameworkVersionsFact("net461")]
        [Trait("Category", "Smoke")]
        [Trait("LoadFromGAC", "True")]
        public void NoExceptions()
        {
            CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
