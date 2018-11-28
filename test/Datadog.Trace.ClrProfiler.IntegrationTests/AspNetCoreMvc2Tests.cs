/*
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

// TODO: add env var ASPNETCORE_URLS = "http://localhost:{HttpServerPort}/"

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class AspNetCoreMvc2Tests : TestHelper
    {
        private const int AgentPort = 9008;
        private const int Port = 9009;

        public AspNetCoreMvc2Tests(ITestOutputHelper output)
            : base("AspNetCoreMvc2", output)
        {
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [InlineData("/")]
        [InlineData("/api/delay/0")]
        public void SubmitsTracesSelfHosted(string path)
        {
            using (var agent = new MockTracerAgent(AgentPort))
            using (Process process = StartSample(AgentPort))
            {
                var wh = new EventWaitHandle(false, EventResetMode.AutoReset);

                process.OutputDataReceived += (sender, args) =>
                                              {
                                                  if (args.Data != null)
                                                  {
                                                      if (args.Data.Contains("Now listening on:") || args.Data.Contains("Unable to start Kestrel"))
                                                      {
                                                          wh.Set();
                                                      }

                                                      Output.WriteLine($"[webserver][stdout] {args.Data}");
                                                  }
                                              };
                process.BeginOutputReadLine();

                process.ErrorDataReceived += (sender, args) =>
                                             {
                                                 if (args.Data != null)
                                                 {
                                                     Output.WriteLine($"[webserver][stderr] {args.Data}");
                                                 }
                                             };
                process.BeginErrorReadLine();

                // wait for server to start
                wh.WaitOne(5000);

                try
                {
                    var request = WebRequest.Create($"http://localhost:{Port}{path}");
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

                var spans = agent.WaitForSpans(1);
                Assert.True(spans.Count > 0, "expected at least one span");

                foreach (var span in spans)
                {
                    Assert.Equal(Integrations.AspNetWebApi2Integration.OperationName, span.Name);
                    Assert.Equal(SpanTypes.Web, span.Type);
                    Assert.Equal($"GET {path}", span.Resource);
                }

                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
        }
    }
}
*/
