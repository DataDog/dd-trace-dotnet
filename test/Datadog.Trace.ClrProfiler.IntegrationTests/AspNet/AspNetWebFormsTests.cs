#if NET461 || NET452

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
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
            : base("WebForms", "samples-aspnet", output)
        {
            SetServiceVersion("1.0.0");

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
            await AssertWebServerSpan(
                path,
                _iisFixture.Agent,
                _iisFixture.HttpPort,
                HttpStatusCode.OK,
                SpanTypes.Web,
                "aspnet.request",
                expectedResourceName,
                "1.0.0");
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        [Trait("Integration", nameof(AspNetWebFormsTests))]
        public async Task NestedAsyncElasticCallSubmitsTrace()
        {
            var testStart = DateTime.UtcNow;
            using (var httpClient = new HttpClient())
            {
                // disable tracing for this HttpClient request
                httpClient.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");

                var response = await httpClient.GetAsync($"http://localhost:{_iisFixture.HttpPort}" + "/Database/Elasticsearch");
                var content = await response.Content.ReadAsStringAsync();
                Output.WriteLine($"[http] {response.StatusCode} {content}");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            var allSpans = _iisFixture.Agent.WaitForSpans(3, minDateTime: testStart)
                                   .OrderBy(s => s.Start)
                                   .ToList();

            Assert.True(allSpans.Count > 0, "Expected there to be spans.");

            var elasticSpans = allSpans
                             .Where(s => s.Type == "elasticsearch")
                             .ToList();

            Assert.True(elasticSpans.Count > 0, "Expected elasticsearch spans.");

            foreach (var span in elasticSpans)
            {
                Assert.Equal("elasticsearch.query", span.Name);
                Assert.Equal("Development Web Site-elasticsearch", span.Service);
                Assert.Equal("elasticsearch", span.Type);
            }
        }
    }
}

#endif
