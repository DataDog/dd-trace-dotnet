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
            : base(iisFixture, output, enableCallTarget: false, enableInlining: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc4WithFeatureFlagTestsCallTargetNoInlining : AspNetMvc4WithFeatureFlagTests
    {
        public AspNetMvc4WithFeatureFlagTestsCallTargetNoInlining(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, enableInlining: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc4WithFeatureFlagTestsCallTarget : AspNetMvc4WithFeatureFlagTests
    {
        public AspNetMvc4WithFeatureFlagTestsCallTarget(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, enableInlining: true)
        {
        }
    }

    public abstract class AspNetMvc4WithFeatureFlagTests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;

        public AspNetMvc4WithFeatureFlagTests(IisFixture iisFixture, ITestOutputHelper output, bool enableCallTarget, bool enableInlining)
            : base("AspNetMvc4", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetCallTargetSettings(enableCallTarget, enableInlining);
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, "true");

            _iisFixture = iisFixture;
            _iisFixture.TryStartIis(this);
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("Integration", nameof(Integrations.AspNetMvcIntegration))]
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
                expectedErrorType,
                expectedErrorMessage,
                "web",
                "aspnet-mvc.request",
                expectedResourceName,
                "1.0.0",
                tags);
        }
    }
}

#endif
