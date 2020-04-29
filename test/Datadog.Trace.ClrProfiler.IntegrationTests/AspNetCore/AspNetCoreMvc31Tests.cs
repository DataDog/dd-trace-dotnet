using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class AspNetCoreMvc31Tests : AspNetCoreMvcTestBase
    {
        public AspNetCoreMvc31Tests(ITestOutputHelper output)
            : base("AspNetCoreMvc31", output)
        {
            // EnableDebugMode();
        }

        [TargetFrameworkVersionsFact("netcoreapp3.1")]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async void MeetsAllAspNetCoreMvcExpectations()
        {
            // No package versions are relevant because this is built-in
            await RunTraceTestOnSelfHosted(string.Empty);
        }
    }
}
