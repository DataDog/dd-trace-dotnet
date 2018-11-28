#if NET461

using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class AspNetMvc5Tests : IisExpressTest
    {
        public AspNetMvc5Tests(ITestOutputHelper output, IisExpressFixture fixture)
            : base("AspNetMvc5", output, fixture)
        {
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("Integration", nameof(Integrations.AspNetMvcIntegration))]
        [InlineData("/Home/Index", "GET home.index")]
        [InlineData("/delay/0", "GET home.delay")]
        [InlineData("/delay-async/0", "GET home.delayasync")]
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
