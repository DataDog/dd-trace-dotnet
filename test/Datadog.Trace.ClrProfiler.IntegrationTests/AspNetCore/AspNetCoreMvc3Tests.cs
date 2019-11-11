using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class AspNetCoreMvc3Tests : AspNetCoreMvcTestBase
    {
        public AspNetCoreMvc3Tests(ITestOutputHelper output)
            : base("AspNetCoreMvc.Netcore3", output)
        {
            EnableDebugMode();
        }

#if NETCOREAPP3_0
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
