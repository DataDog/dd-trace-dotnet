using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class NLog10LogsInjectionNullReferenceExceptionSmokeTest : SmokeTestBase
    {
        public NLog10LogsInjectionNullReferenceExceptionSmokeTest(ITestOutputHelper output)
            : base(output, "NLog10LogsInjection.NullReferenceException")
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
        }

        [TargetFrameworkVersionsFact("net452;net461")]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
