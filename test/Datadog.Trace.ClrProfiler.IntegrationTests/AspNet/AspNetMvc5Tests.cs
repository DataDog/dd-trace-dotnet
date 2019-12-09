#if NET461

using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection("IisTests")]
    [Trait("Category", "IISTests")]
    public class AspNetMvc5Tests : TestHelper, IClassFixture<IISFixture>
    {
        private readonly IISFixture _iisFixture;

        public AspNetMvc5Tests(IISFixture iisFixture, ITestOutputHelper output)
            : base("AspNetMvc5", "samples-aspnet", output)
        {
            _iisFixture = iisFixture;
            _iisFixture.TryConnectIIS();
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
                "Samples.AspNetMvc5",
                path,
                _iisFixture.Agent,
                HttpStatusCode.OK,
                "web",
                "aspnet-mvc.request",
                $"{expectedVerb} {expectedResourceSuffix}");
        }
    }
}

#endif
