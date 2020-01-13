using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class AspNetCoreMvc2Tests : AspNetCoreMvcTestBase
    {
        public AspNetCoreMvc2Tests(ITestOutputHelper output)
            : base("AspNetCoreMvc2", output)
        {
        }

        [TargetFrameworkVersionsFact("netcoreapp2.1")]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void MeetsAllAspNetCoreMvcExpectations()
        {
            // No package versions are relevant because this is built-in
            RunTraceTestOnSelfHosted(string.Empty);
        }
    }
}
