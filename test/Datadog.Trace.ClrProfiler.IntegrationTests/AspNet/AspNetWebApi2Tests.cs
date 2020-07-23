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
            : base("AspNetMvc5", "samples-aspnet", output)
        {
            SetServiceVersion("1.0.0");

            _iisFixture = iisFixture;
            _iisFixture.TryStartIis(this);
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("Integration", nameof(Integrations.AspNetWebApi2Integration))]
        [InlineData("/api/environment", "GET api/environment", HttpStatusCode.OK, false)]
        [InlineData("/api/delay/0", "GET api/delay/{seconds}", HttpStatusCode.OK, false)]
        [InlineData("/api/delay-async/0", "GET api/delay-async/{seconds}", HttpStatusCode.OK, false)]
        [InlineData("/api/transient-failure/true", "GET api/transient-failure/{value}", HttpStatusCode.OK, false)]
        [InlineData("/api/transient-failure/false", "GET api/transient-failure/{value}", HttpStatusCode.InternalServerError, true)]
        [InlineData("/api/statuscode/201", "GET api/statuscode/{value}", HttpStatusCode.Created, false)]
        [InlineData("/api/statuscode/503", "GET api/statuscode/{value}", HttpStatusCode.ServiceUnavailable, true)]
        public async Task SubmitsTraces(
            string path,
            string expectedResourceName,
            HttpStatusCode expectedStatusCode,
            bool isError)
        {
            await AssertWebServerSpan(
                path,
                _iisFixture.Agent,
                _iisFixture.HttpPort,
                expectedStatusCode,
                isError,
                "web",
                "aspnet-webapi.request",
                expectedResourceName,
                "1.0.0");
        }
    }
}

#endif
