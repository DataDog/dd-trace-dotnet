#if NETCOREAPP2_1

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

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class AspNetCoreMvc2Tests : TestHelper
    {
        private static readonly string _topLevelOperationName = "aspnet-coremvc.request";

        private static readonly List<WebServerSpanExpectation> _expectations = new List<WebServerSpanExpectation>()
        {
            CreateTopLevelExpectation(url: "/", httpMethod: "GET", httpStatus: "200", resourceUrl: "/"),
            CreateTopLevelExpectation(url: "/delay/0", httpMethod: "GET", httpStatus: "200", resourceUrl: "delay/{seconds}"),
            CreateTopLevelExpectation(url: "/api/delay/0", httpMethod: "GET", httpStatus: "200", resourceUrl: "api/delay/{seconds}"),
            CreateTopLevelExpectation(url: "/status-code/203", httpMethod: "GET", httpStatus: "203", resourceUrl: "status-code/{statusCode}"),
            // TODO: The below test succeeds in IISExpress, but fails in self host when expecting a status code of 500.
            CreateTopLevelExpectation(
                url: "/bad-request",
                httpMethod: "GET",
                httpStatus: null,
                resourceUrl: "bad-request",
                additionalCheck: span =>
                {
                    var failures = new List<string>();
                    if (span.Tags[Tags.ErrorMsg] != "This was a bad request.")
                    {
                        failures.Add($"Expected specific exception within {span.Resource}");
                    }

                    return failures;
                }),
        };

        public AspNetCoreMvc2Tests(ITestOutputHelper output)
            : base("AspNetCoreMvc2", output)
        {
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
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

                var paths = _expectations.Select(e => e.OriginalUri).ToArray();
                SubmitRequests(aspNetCorePort, paths);
                var spans = agent.WaitForSpans(_expectations.Count, operationName: _topLevelOperationName, returnAllOperations: true)
                                 .OrderBy(s => s.Start)
                                 .ToList();

                if (!process.HasExited)
                {
                    process.Kill();
                }

                SpanTestHelpers.AssertExpectationsMet(_expectations, spans);
            }
        }

        private static WebServerSpanExpectation CreateTopLevelExpectation(
            string url,
            string httpMethod,
            string httpStatus,
            string resourceUrl,
            Func<MockTracerAgent.Span, List<string>> additionalCheck = null)
        {
            var expectation = new WebServerSpanExpectation("Samples.AspNetCoreMvc2", _topLevelOperationName)
            {
                OriginalUri = url,
                HttpMethod = httpMethod,
                ResourceName = $"{httpMethod.ToUpper()} {resourceUrl}",
                StatusCode = httpStatus,
            };

            expectation.RegisterDelegateExpectation(additionalCheck);

            return expectation;
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
                        string responseText;
                        try
                        {
                            responseText = reader.ReadToEnd();
                        }
                        catch (Exception ex)
                        {
                            responseText = "ENCOUNTERED AN ERROR WHEN READING RESPONSE.";
                            Output.WriteLine(ex.ToString());
                        }

                        Output.WriteLine($"[http] {response.StatusCode} {responseText}");
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

#endif
