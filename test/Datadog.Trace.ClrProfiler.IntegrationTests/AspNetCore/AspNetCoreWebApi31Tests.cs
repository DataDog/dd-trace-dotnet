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
            // TODO: When the automatic instrumentation begins to create traces from this sample,
            // uncomment and remove the requestPathsOverride in MeetsAllAspNetCoreMvcExpectations
            // CreateTopLevelExpectation(url: "/WeatherForecast", httpMethod: "GET", httpStatus: "200", resourceUrl: "WeatherForecast");
        }

        // TODO: Remove Skip property to run the sample in CI
        [TargetFrameworkVersionsFact("netcoreapp3.1")]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void MeetsAllAspNetCoreMvcExpectations()
        {
            // No package versions are relevant because this is built-in
            RunTraceTestOnSelfHosted(string.Empty, requestPathsOverride: new[] { "/WeatherForecast" });
        }
    }
}
