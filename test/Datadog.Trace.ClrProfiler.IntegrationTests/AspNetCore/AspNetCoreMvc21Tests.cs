using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class AspNetCoreMvc21Tests : AspNetCoreMvcTestBase
    {
        public AspNetCoreMvc21Tests(ITestOutputHelper output)
            : base("AspNetCoreMvc21", output, serviceVersion: "1.0.0")
        {
        }

        [TargetFrameworkVersionsFact("netcoreapp2.1")]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task MeetsAllAspNetCoreMvcExpectations()
        {
            // No package versions are relevant because this is built-in
            await RunTraceTestOnSelfHosted(string.Empty);
        }
    }
}
