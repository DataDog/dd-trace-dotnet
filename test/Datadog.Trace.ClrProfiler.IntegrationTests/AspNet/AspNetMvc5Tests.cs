#if NET461
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection("IisTests")]
    public class AspNetMvc5TestsCallsiteClassic : AspNetMvc5Tests
    {
        public AspNetMvc5TestsCallsiteClassic(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false, classicMode: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5TestsCallsiteIntegrated : AspNetMvc5Tests
    {
        public AspNetMvc5TestsCallsiteIntegrated(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false, classicMode: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5TestsCallTargetClassic : AspNetMvc5Tests
    {
        public AspNetMvc5TestsCallTargetClassic(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, classicMode: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5TestsCallTargetIntegrated : AspNetMvc5Tests
    {
        public AspNetMvc5TestsCallTargetIntegrated(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, classicMode: false)
        {
        }
    }

    public abstract class AspNetMvc5Tests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;

        public AspNetMvc5Tests(IisFixture iisFixture, ITestOutputHelper output, bool enableCallTarget, bool classicMode)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetCallTargetSettings(enableCallTarget);

            _iisFixture = iisFixture;
            _iisFixture.TryStartIis(this, classicMode);
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [MemberData(nameof(AspNetMvc5TestData.WithoutFeatureFlag), MemberType = typeof(AspNetMvc5TestData))]
        public async Task SubmitsTraces(
            string path,
            string expectedAspNetResourceName,
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
                expectedAspNetResourceName,
                expectedResourceName,
                "1.0.0",
                expectedTags);
        }
    }
}
#endif
