#if NET461

using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class AspNetWebApi2Tests : IisExpressTest
    {
        public AspNetWebApi2Tests(ITestOutputHelper output, IisExpressFixture fixture)
            : base("AspNetMvc5", output, fixture)
        {
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("Integration", nameof(Integrations.AspNetWebApi2Integration))]
        [InlineData("/api/environment", "GET api/environment")]
        [InlineData("/api/delay/0", "GET api/delay/{seconds}")]
        [InlineData("/api/delay-async/0", "GET api/delay-async/{seconds}")]
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
                Integrations.AspNetWebApi2Integration.OperationName,
                expectedResourceName);
        }
    }
}

#endif
