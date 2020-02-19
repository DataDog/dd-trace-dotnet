using Datadog.Core.Tools;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class StackExchangeRedisAssemblyConflictLegacyProjectSmokeTest : SmokeTestBase
    {
        public StackExchangeRedisAssemblyConflictLegacyProjectSmokeTest(ITestOutputHelper output)
            : base(output, "StackExchange.Redis.AssemblyConflict.LegacyProject", maxTestRunSeconds: 15)
        {
        }

        [TargetFrameworkVersionsFact("net452;net461")]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            if (!EnvironmentTools.IsWindows())
            {
                Output.WriteLine("Ignored for Linux");
                return;
            }

            CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
