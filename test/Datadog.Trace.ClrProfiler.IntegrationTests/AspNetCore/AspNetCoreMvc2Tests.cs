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

#if NETCOREAPP2_1
        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(PackageVersions.AspNetCoreMvc2), MemberType = typeof(PackageVersions))]
        public void MeetsAllAspNetCoreMvcExpectations(string packageVersion)
        {
            RunTraceTestOnSelfHosted(packageVersion);
        }
#endif
    }
}
