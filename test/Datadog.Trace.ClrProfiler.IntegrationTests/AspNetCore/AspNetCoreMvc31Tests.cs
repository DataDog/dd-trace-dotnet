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

#if NETCOREAPP3_1
        [Fact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void MeetsAllAspNetCoreMvcExpectations()
        {
            // No package versions are relevant because this is built-in
            RunTraceTestOnSelfHosted(string.Empty);
        }
#endif
    }
}
