#if NET461

using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class AspNetMvc4Tests : TestHelper, IClassFixture<IISExpressFixture>
    {
        private readonly IISExpressFixture _iisFixture;

        public AspNetMvc4Tests(IISExpressFixture iisFixture, ITestOutputHelper output)
            : base("AspNetMvc4", "samples-aspnet", output)
        {
            _iisFixture = iisFixture;
            _iisFixture.TryStartIISExpress(this);
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("Integration", nameof(Integrations.AspNetMvcIntegration))]
        [InlineData("/Home/Index", "GET /home/index")]
        public async Task SubmitsTraces(
            string path,
            string expectedResourceName)
        {
            await AssertHttpSpan(
                path,
                _iisFixture.Agent,
                _iisFixture.HttpPort,
                HttpStatusCode.OK,
                "web",
                "aspnet-mvc.request",
                expectedResourceName);
        }
    }
}

#endif
