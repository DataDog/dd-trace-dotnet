#if NET461
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection("IisTests")]
    public class AspNetWebApi2TestsCallsite : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallsite(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false, enableInlining: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2TestsCallTargetNoInlining : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallTargetNoInlining(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, enableInlining: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2TestsCallTarget : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallTarget(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, enableInlining: true)
        {
        }
    }

    public abstract class AspNetWebApi2Tests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;

        public AspNetWebApi2Tests(IisFixture iisFixture, ITestOutputHelper output, bool enableCallTarget, bool enableInlining)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetCallTargetSettings(enableCallTarget, enableInlining);

            _iisFixture = iisFixture;
            _iisFixture.TryStartIis(this);
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("Integration", nameof(Integrations.AspNetWebApi2Integration))]
        [MemberData(nameof(AspNetWebApi2TestData.Data), MemberType = typeof(AspNetWebApi2TestData))]
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
                expectedErrorType,
                expectedErrorMessage,
                "web",
                "aspnet-webapi.request",
                expectedResourceName,
                "1.0.0",
                expectedTags);
        }
    }
}

#endif
