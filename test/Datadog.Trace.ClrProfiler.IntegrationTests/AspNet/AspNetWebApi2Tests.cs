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
    public class AspNetWebApi2TestsCallsiteClassic : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallsiteClassic(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false, classicMode: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2TestsCallsiteIntegrated : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallsiteIntegrated(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false, classicMode: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2TestsCallTargetClassic : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallTargetClassic(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, classicMode: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2TestsCallTargetIntegrated : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallTargetIntegrated(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, classicMode: false)
        {
        }
    }

    public abstract class AspNetWebApi2Tests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;

        public AspNetWebApi2Tests(IisFixture iisFixture, ITestOutputHelper output, bool enableCallTarget, bool classicMode)
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
        [MemberData(nameof(AspNetWebApi2TestData.WithoutFeatureFlag), MemberType = typeof(AspNetWebApi2TestData))]
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
                expectedAspNetErrorType: null,
                expectedAspNetErrorMessage: isError ? $"The HTTP response has status code {(int)expectedStatusCode}." : null,
                expectedErrorType: expectedErrorType,
                expectedErrorMessage: expectedErrorMessage,
                "web",
                "aspnet-webapi.request",
                expectedAspNetResourceName,
                expectedResourceName,
                "1.0.0",
                expectedTags);
        }
    }
}

#endif
