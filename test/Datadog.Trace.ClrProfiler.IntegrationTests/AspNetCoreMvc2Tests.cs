using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class AspNetCoreMvc2Tests : TestHelper
    {
        private static readonly string[] Paths = { "/", "/api/delay/0" };

        public AspNetCoreMvc2Tests(ITestOutputHelper output)
            : base("AspNetCoreMvc2", output)
        {
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [MemberData(nameof(PackageVersions.AspNetCoreMvc2), MemberType = typeof(PackageVersions))]
        public void SubmitsTracesSelfHosted(string packageVersion)
        {
            int agentPort = TcpPortProvider.GetOpenPort();
            int aspNetCorePort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (Process process = StartSample(agent.Port, arguments: null, packageVersion: packageVersion, aspNetCorePort: aspNetCorePort))
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

                SubmitRequests(aspNetCorePort, Paths);
                var expected = new List<string>(Paths);
                var spans = agent.WaitForSpans(expected.Count)
                                 .Where(s => s.Type == SpanTypes.Web)
                                 .OrderBy(s => s.Start)
                                 .ToList();

                if (!process.HasExited)
                {
                    process.Kill();
                }

                Assert.True(spans.Count >= expected.Count, $"expected at least {expected.Count} spans");
                foreach (var span in spans)
                {
                    Assert.Equal("aspnet-coremvc.request", span.Name);
                    Assert.Equal("Samples.AspNetCoreMvc2", span.Service);
                    Assert.Equal(SpanTypes.Web, span.Type);
                }

                ValidateSpans(spans, (span) => span.Resource, expected);
            }
        }

        private void SubmitRequests(int aspNetCorePort, string[] paths)
        {
            foreach (string path in paths)
            {
                try
                {
                    var request = WebRequest.Create($"http://localhost:{aspNetCorePort}{path}");
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
        }
    }
}
