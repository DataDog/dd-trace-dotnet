#if NET461
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection("IisTests")]
    public class AspNetMvc4WithFeatureFlagTestsCallsite : AspNetMvc4WithFeatureFlagTests
    {
        public AspNetMvc4WithFeatureFlagTestsCallsite(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc4WithFeatureFlagTestsCallTarget : AspNetMvc4WithFeatureFlagTests
    {
        public AspNetMvc4WithFeatureFlagTestsCallTarget(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true)
        {
        }
    }

    public abstract class AspNetMvc4WithFeatureFlagTests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;

        public AspNetMvc4WithFeatureFlagTests(IisFixture iisFixture, ITestOutputHelper output, bool enableCallTarget)
            : base("AspNetMvc4", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetCallTargetSettings(enableCallTarget);
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, "true");

            _iisFixture = iisFixture;
            _iisFixture.TryStartIis(this);
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [MemberData(nameof(AspNetMvc4TestData.WithFeatureFlag), MemberType = typeof(AspNetMvc4TestData))]
        public async Task WithNewResourceNames_SubmitsTraces(
            string path,
            string expectedResourceName,
            HttpStatusCode expectedStatusCode,
            bool isError,
            string expectedErrorType,
            string expectedErrorMessage,
            SerializableDictionary tags)
        {
            await AssertWebServerSpan(
                path,
                _iisFixture.Agent,
                _iisFixture.HttpPort,
                expectedStatusCode,
                isError,
                expectedAspNetErrorType: expectedErrorType,
                expectedAspNetErrorMessage: expectedErrorMessage,
                expectedErrorType: expectedErrorType,
                expectedErrorMessage: expectedErrorMessage,
                "web",
                "aspnet-mvc.request",
                expectedResourceName,
                expectedResourceName,
                "1.0.0",
                tags);
        }
    }
}

#endif
