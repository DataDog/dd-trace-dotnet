using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using Datadog.Trace.TestHelpers;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public abstract class AspNetCoreMvcTestBase : TestHelper
    {
        protected static readonly string TopLevelOperationName = "aspnet-coremvc.request";

        protected AspNetCoreMvcTestBase(string sampleAppName, ITestOutputHelper output)
            : base(sampleAppName, output)
        {
            CreateTopLevelExpectation(url: "/", httpMethod: "GET", httpStatus: "200", resourceUrl: "/");
            CreateTopLevelExpectation(url: "/delay/0", httpMethod: "GET", httpStatus: "200", resourceUrl: "delay/{seconds}");
            CreateTopLevelExpectation(url: "/api/delay/0", httpMethod: "GET", httpStatus: "200", resourceUrl: "api/delay/{seconds}");
            CreateTopLevelExpectation(url: "/status-code/203", httpMethod: "GET", httpStatus: "203", resourceUrl: "status-code/{statusCode}");
            CreateTopLevelExpectation(
                url: "/bad-request",
                httpMethod: "GET",
                httpStatus: null, // TODO: Enable status code tests
                resourceUrl: "bad-request",
                additionalCheck: span =>
                {
                    var failures = new List<string>();
                    if (SpanExpectation.GetTag(span, Tags.ErrorMsg) != "This was a bad request.")
                    {
                        failures.Add($"Expected specific exception within {span.Resource}");
                    }

                    return failures;
                });
        }

        protected List<AspNetCoreMvcSpanExpectation> Expectations { get; set; } = new List<AspNetCoreMvcSpanExpectation>();

        public void RunTraceTestOnSelfHosted(string packageVersion)
        {
            var agentPort = TcpPortProvider.GetOpenPort();
            var aspNetCorePort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (var process = StartSample(agent.Port, arguments: null, packageVersion: packageVersion, aspNetCorePort: aspNetCorePort))
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

                wh.WaitOne(5000);

                var maxTimesToCheck = 20;

                // wait for server to be ready to receive requests
                while (true)
                {
                    maxTimesToCheck--;

                    if (maxTimesToCheck <= 0)
                    {
                        throw new Exception("Unable to verify whether the server is ready to receive requests.");
                    }

                    var ready = SubmitRequest(aspNetCorePort, "/alive-check");

                    Thread.Sleep(300);

                    if (ready)
                    {
                        break;
                    }
                }

                var testStart = DateTime.Now;

                var paths = Expectations.Select(e => e.OriginalUri).ToArray();
                SubmitRequests(aspNetCorePort, paths);

                var spans =
                    agent.WaitForSpans(
                              Expectations.Count,
                              operationName: TopLevelOperationName,
                              minDateTime: testStart)
                         .OrderBy(s => s.Start)
                         .ToList();

                if (!process.HasExited)
                {
                    process.Kill();
                }

                SpanTestHelpers.AssertExpectationsMet(Expectations, spans);
            }
        }

        protected void CreateTopLevelExpectation(
            string url,
            string httpMethod,
            string httpStatus,
            string resourceUrl,
            Func<MockTracerAgent.Span, List<string>> additionalCheck = null)
        {
            var expectation = new AspNetCoreMvcSpanExpectation(EnvironmentHelper.FullSampleName, TopLevelOperationName)
            {
                OriginalUri = url,
                HttpMethod = httpMethod,
                ResourceName = $"{httpMethod.ToUpper()} {resourceUrl}",
                StatusCode = httpStatus,
            };

            expectation.RegisterDelegateExpectation(additionalCheck);

            Expectations.Add(expectation);
        }

        protected void SubmitRequests(int aspNetCorePort, string[] paths)
        {
            foreach (var path in paths)
            {
                SubmitRequest(aspNetCorePort, path);
            }
        }

        protected bool SubmitRequest(int aspNetCorePort, string path)
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

                return false;
            }

            return true;
        }
    }
}
