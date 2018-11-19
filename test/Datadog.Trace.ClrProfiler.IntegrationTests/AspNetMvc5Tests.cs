#if NET461

using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class AspNetMvc5Tests : TestHelper
    {
        private const int AgentPort = 9000;
        private const int Port = 9001;

        public AspNetMvc5Tests(ITestOutputHelper output)
            : base("AspNetMvc5", output)
        {
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTraces()
        {
            using (var agent = new MockTracerAgent(AgentPort))
            {
                using (var iis = StartIISExpress(AgentPort, Port))
                {
                    // give IIS Express time to boot up
                    await Task.Delay(2000);

                    var httpClient = new HttpClient();
                    HttpResponseMessage response = await httpClient.GetAsync($"http://localhost:{Port}/api/environment");
                    response.EnsureSuccessStatusCode();

                    string content = await response.Content.ReadAsStringAsync();
                    Output.WriteLine($"[http] {response.StatusCode} {content}");
                }

                var spans = agent.WaitForSpans(1);
                Assert.True(spans.Count > 0, "expected at least one span");

                foreach (var span in spans)
                {
                    Assert.Equal(Integrations.AspNetWebApi2Integration.OperationName, span.Name);
                    Assert.Equal(SpanTypes.Web, span.Type);
                    Assert.Equal("GET api/environment", span.Resource);
                }
            }
        }
    }
}

#endif
