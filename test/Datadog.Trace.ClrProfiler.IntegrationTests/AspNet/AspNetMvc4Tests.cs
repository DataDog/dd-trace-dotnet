#if NET461

using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class AspNetMvc4Tests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;

        public AspNetMvc4Tests(IisFixture iisFixture, ITestOutputHelper output)
            : base("AspNetMvc4", "samples-aspnet", output)
        {
            SetServiceVersion("1.0.0");

            _iisFixture = iisFixture;
            _iisFixture.TryStartIis(this);
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("Integration", nameof(Integrations.AspNetMvcIntegration))]
        [InlineData("/Home/Index", "GET /home/index", HttpStatusCode.OK)]
        [InlineData("/Home/BadRequest", "GET /home/badrequest", HttpStatusCode.InternalServerError)]
        [InlineData("/Home/StatusCode?value=201", "GET /home/statuscode", HttpStatusCode.Created)]
        public async Task SubmitsTraces(
            string path,
            string expectedResourceName,
            HttpStatusCode expectedStatusCode)
        {
            await AssertWebServerSpan(
                path,
                _iisFixture.Agent,
                _iisFixture.HttpPort,
                expectedStatusCode,
                "web",
                "aspnet-mvc.request",
                expectedResourceName,
                "1.0.0");
        }
    }
}

#endif
