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
    [CollectionDefinition("IisTests", DisableParallelization = true)]
    [Collection("IisTests")]
    public class AspNetMvc4TestsCallsiteClassic : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallsiteClassic(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false, classicMode: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc4TestsCallsiteIntegrated : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallsiteIntegrated(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false, classicMode: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc4TestsCallTargetClassic : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallTargetClassic(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, classicMode: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc4TestsCallTargetIntegrated : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallTargetIntegrated(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, classicMode: false)
        {
        }
    }

    public abstract class AspNetMvc4Tests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;

        public AspNetMvc4Tests(IisFixture iisFixture, ITestOutputHelper output, bool enableCallTarget, bool classicMode)
            : base("AspNetMvc4", @"test\test-applications\aspnet", output)
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
        [MemberData(nameof(AspNetMvc4TestData.WithoutFeatureFlag), MemberType = typeof(AspNetMvc4TestData))]
        public async Task SubmitsTraces(
            string path,
            string expectedAspNetResourceName,
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
                expectedAspNetResourceName,
                expectedResourceName,
                "1.0.0",
                tags);
        }
    }
}
#endif
