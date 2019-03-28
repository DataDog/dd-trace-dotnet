#if NET461 || NET452

using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class AspNetWebFormsTests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;

        // NOTE: Would pass this in addition to the name/output to the new constructor if we removed the Samples.WebForms copied project in favor of the demo repo source project...
        // $"../dd-trace-demo/dotnet-coffeehouse/Datadog.Coffeehouse.WebForms",
        public AspNetWebFormsTests(IisFixture iisFixture, ITestOutputHelper output)
            : base("WebForms", "samples-aspnet/Samples.WebForms", output)
        {
            _iisFixture = iisFixture;
            _iisFixture.TryStartIis(this);
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("Integration", nameof(AspNetWebFormsTests))]
        [InlineData("/Account/Login", "GET /account/login")]
        public async Task SubmitsTraces(
            string path,
            string expectedResourceName)
        {
            await AssertHttpSpan(
                                 path,
                                 _iisFixture.AgentPort,
                                 _iisFixture.HttpPort,
                                 HttpStatusCode.OK,
                                 SpanTypes.Web,
                                 "aspnet-web-forms.request",
                                 expectedResourceName);
        }
    }
}

#endif
