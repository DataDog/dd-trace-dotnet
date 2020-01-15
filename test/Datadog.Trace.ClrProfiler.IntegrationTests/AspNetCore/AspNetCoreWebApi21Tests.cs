using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class AspNetCoreWebApi21Tests : AspNetCoreMvcTestBase
    {
        public AspNetCoreWebApi21Tests(ITestOutputHelper output)
            : base("AspNetCoreWebApi21", output)
        {
            Expectations.Clear();
            CreateTopLevelExpectation(url: "/api/values", httpMethod: "GET", httpStatus: "200", resourceUrl: "api/values");
            CreateTopLevelExpectation(url: "/api/values/5", httpMethod: "GET", httpStatus: "200", resourceUrl: "api/values/{id}");
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
