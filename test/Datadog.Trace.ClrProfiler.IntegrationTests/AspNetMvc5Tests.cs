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
            : base("AspNetMvc5", "samples-aspnet", output)
        {
            _iisFixture = iisFixture;
            _iisFixture.TryStartIis(this);
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("Integration", nameof(Integrations.AspNetMvcIntegration))]
        [InlineData("/Home/Index", "GET", "/home/index")]
        [InlineData("/delay/0", "GET", "/delay/{seconds}")]
        [InlineData("/delay-async/0", "GET", "/delay-async/{seconds}")]
        public async Task SubmitsTraces(
            string path,
            string expectedVerb,
            string expectedResourceSuffix)
        {
            await AssertHttpSpan(
                path,
                _iisFixture.Agent,
                _iisFixture.HttpPort,
                HttpStatusCode.OK,
                "web",
                "aspnet-mvc.request",
                $"{expectedVerb} {expectedResourceSuffix}");
        }
    }
}

#endif
