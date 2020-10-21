#if NET461

using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection("IisTests")]
    public class AspNetMvc5Tests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;

        public AspNetMvc5Tests(IisFixture iisFixture, ITestOutputHelper output)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");

            _iisFixture = iisFixture;
            _iisFixture.TryStartIis(this);
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("Integration", nameof(Integrations.AspNetMvcIntegration))]
        [InlineData("/Home/Index", "GET", "/home/index", HttpStatusCode.OK, false, null, null)]
        [InlineData("/delay/0", "GET", "/delay/{seconds}", HttpStatusCode.OK, false, null, null)]
        [InlineData("/delay-async/0", "GET", "/delay-async/{seconds}", HttpStatusCode.OK, false, null, null)]
        [InlineData("/badrequest", "GET", "/badrequest", HttpStatusCode.InternalServerError, true, null, null)]
        [InlineData("/statuscode/201", "GET", "/statuscode/{value}", HttpStatusCode.Created, false, null, null)]
        [InlineData("/statuscode/503", "GET", "/statuscode/{value}", HttpStatusCode.ServiceUnavailable, true, null, "The HTTP response has status code 503.")]
        public async Task SubmitsTraces(
            string path,
            string expectedVerb,
            string expectedResourceSuffix,
            HttpStatusCode expectedStatusCode,
            bool isError,
            string expectedErrorType,
            string expectedErrorMessage)
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
                $"{expectedVerb} {expectedResourceSuffix}",
                "1.0.0");
        }
    }
}

#endif
