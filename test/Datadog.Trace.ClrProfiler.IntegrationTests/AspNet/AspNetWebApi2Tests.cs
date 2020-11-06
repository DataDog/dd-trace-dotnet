#if NET461

using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection("IisTests")]
    public class AspNetWebApi2Tests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;

        public AspNetWebApi2Tests(IisFixture iisFixture, ITestOutputHelper output)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");

            _iisFixture = iisFixture;
            _iisFixture.TryStartIis(this);
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("Integration", nameof(Integrations.AspNetWebApi2Integration))]
        [InlineData("/api/environment", "GET api/environment", HttpStatusCode.OK, false, null, null)]
        [InlineData("/api/delay/0", "GET api/delay/{seconds}", HttpStatusCode.OK, false, null, null)]
        [InlineData("/api/delay-async/0", "GET api/delay-async/{seconds}", HttpStatusCode.OK, false, null, null)]
        [InlineData("/api/transient-failure/true", "GET api/transient-failure/{value}", HttpStatusCode.OK, false, null, null)]
        [InlineData("/api/transient-failure/false", "GET api/transient-failure/{value}", HttpStatusCode.InternalServerError, true, null, null)]
        [InlineData("/api/statuscode/201", "GET api/statuscode/{value}", HttpStatusCode.Created, false, null, null)]
        [InlineData("/api/statuscode/503", "GET api/statuscode/{value}", HttpStatusCode.ServiceUnavailable, true, null, "The HTTP response has status code 503.")]
        public async Task SubmitsTraces(
            string path,
            string expectedResourceName,
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
                "aspnet-webapi.request",
                expectedResourceName,
                "1.0.0");
        }
    }
}

#endif
