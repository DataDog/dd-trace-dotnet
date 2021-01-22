#if NETCOREAPP3_1
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class AspNetCoreMvc31Tests : AspNetCoreMvcTestBase
    {
        public AspNetCoreMvc31Tests(ITestOutputHelper output)
            : base("AspNetCoreMvc31", output, serviceVersion: "1.0.0")
        {
            // EnableDebugMode();
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task MeetsAllAspNetCoreMvcExpectations()
        {
            // No package versions are relevant because this is built-in
            await RunTraceTestOnSelfHosted(string.Empty);
        }
    }
}
#endif
