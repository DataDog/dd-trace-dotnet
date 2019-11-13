using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class Log4NetSerializationExceptionSmokeTest : SmokeTestBase
    {
        public Log4NetSerializationExceptionSmokeTest(ITestOutputHelper output)
            : base(output, "Log4Net.SerializationException", maxTestRunSeconds: 60)
        {
        }

        [TargetFrameworkVersionsFact("net452;net461")]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            CheckForSmoke();
        }
    }
}
