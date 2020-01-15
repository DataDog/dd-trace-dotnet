using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class AspNetCoreWebApi31Tests : AspNetCoreMvcTestBase
    {
        public AspNetCoreWebApi31Tests(ITestOutputHelper output)
            : base("AspNetCoreWebApi31", output)
        {
            Expectations.Clear();
            CreateTopLevelExpectation(url: "/WeatherForecast", httpMethod: "GET", httpStatus: "200", resourceUrl: "WeatherForecast");
        }

        [TargetFrameworkVersionsFact("netcoreapp3.1")]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void MeetsAllAspNetCoreMvcExpectations()
        {
            // No package versions are relevant because this is built-in
            RunTraceTestOnSelfHosted(string.Empty);
        }
    }
}
