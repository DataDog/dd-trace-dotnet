using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
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
        public void SubmitsTraces()
        {
            using (var agent = new MockTracerAgent(AgentPort))
            {
                using (var iis = StartIISExpress(AgentPort, Port))
                {
                    try
                    {
                        var request = WebRequest.Create($"http://localhost:{Port}/api/environment");
                        using (var response = (HttpWebResponse)request.GetResponse())
                        using (var stream = response.GetResponseStream())
                        using (var reader = new StreamReader(stream))
                        {
                            Output.WriteLine($"[http] {response.StatusCode} {reader.ReadToEnd()}");
                        }
                    }
                    catch (WebException wex)
                    {
                        Output.WriteLine($"[http] exception: {wex}");
                        if (wex.Response is HttpWebResponse response)
                        {
                            using (var stream = response.GetResponseStream())
                            using (var reader = new StreamReader(stream))
                            {
                                Output.WriteLine($"[http] {response.StatusCode} {reader.ReadToEnd()}");
                            }
                        }
                    }
                }

                var spans = agent.WaitForSpans(1);
                Assert.True(spans.Count > 0, "expected at least one span");
                foreach (var span in spans)
                {
                    Assert.Equal("aspnet_web.query", span.Name);
                    Assert.Equal("web", span.Type);
                    Assert.Equal("GET api/environment", span.Resource);
                }
            }
        }
    }
}
