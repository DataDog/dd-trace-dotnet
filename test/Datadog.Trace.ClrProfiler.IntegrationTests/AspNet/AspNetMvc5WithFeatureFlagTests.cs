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
    public class AspNetMvc5WithFeatureFlagCallsite : AspNetMvc5WithFeatureFlagTests
    {
        public AspNetMvc5WithFeatureFlagCallsite(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5WithFeatureFlagCallTarget : AspNetMvc5WithFeatureFlagTests
    {
        public AspNetMvc5WithFeatureFlagCallTarget(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true)
        {
        }
    }

    public abstract class AspNetMvc5WithFeatureFlagTests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;

        public AspNetMvc5WithFeatureFlagTests(IisFixture iisFixture, ITestOutputHelper output, bool enableCallTarget)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, "true");
            SetCallTargetSettings(enableCallTarget);

            _iisFixture = iisFixture;
            _iisFixture.TryStartIis(this);
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [MemberData(nameof(AspNetMvc5TestData.WithFeatureFlag), MemberType = typeof(AspNetMvc5TestData))]
        public async Task SubmitsTraces(
            string path,
            string expectedResourceName,
            HttpStatusCode expectedStatusCode,
            bool isError,
            string expectedErrorType,
            string expectedErrorMessage,
            SerializableDictionary expectedTags)
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
                expectedTags);
        }
    }
}

#endif
