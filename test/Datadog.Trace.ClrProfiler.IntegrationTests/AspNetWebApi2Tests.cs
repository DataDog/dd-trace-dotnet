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
            : base("AspNetMvc5", output)
        {
            _iisFixture = iisFixture;
            _iisFixture.TryStartIis(this);
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
                _iisFixture.Agent,
                _iisFixture.HttpPort,
                HttpStatusCode.OK,
                SpanTypes.Web,
                Integrations.AspNetWebApi2Integration.OperationName,
                expectedResourceName);
        }
    }
}

#endif
