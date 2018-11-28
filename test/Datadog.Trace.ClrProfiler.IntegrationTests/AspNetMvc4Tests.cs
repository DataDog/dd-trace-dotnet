#if NET461

using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class AspNetMvc4Tests : IisExpressTest
    {
        public AspNetMvc4Tests(ITestOutputHelper output, IisExpressFixture fixture)
            : base("AspNetMvc4", output, fixture)
        {
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("Integration", nameof(Integrations.AspNetMvcIntegration))]
        [InlineData("/Home/Index", "GET home.index")]
        public async Task SubmitsTraces(
            string path,
            string expectedResourceName)
        {
            await AssertHttpSpan(
                path,
                Fixture.AgentPort,
                Fixture.HttpPort,
                HttpStatusCode.OK,
                SpanTypes.Web,
                Integrations.AspNetMvcIntegration.OperationName,
                expectedResourceName);
        }
    }
}

#endif
