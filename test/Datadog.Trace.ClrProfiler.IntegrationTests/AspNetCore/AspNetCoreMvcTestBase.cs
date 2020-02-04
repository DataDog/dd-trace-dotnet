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
        protected static readonly string TopLevelOperationName = "aspnet_core.request";

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
                httpStatus: "500",
                resourceUrl: "bad-request",
                additionalCheck: span =>
                {
                    var failures = new List<string>();

                    if (span.Error == 0)
                    {
                        failures.Add($"Expected Error flag set within {span.Resource}");
                    }

                    if (SpanExpectation.GetTag(span, Tags.ErrorType) != "System.Exception")
                    {
                        failures.Add($"Expected specific exception within {span.Resource}");
                    }

                    var errorMessage = SpanExpectation.GetTag(span, Tags.ErrorMsg);

                    if (errorMessage != "This was a bad request.")
                    {
                        failures.Add($"Expected specific error message within {span.Resource}. Found \"{errorMessage}\"");
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
                agent.SpanFilters.Add(IsNotServerLifeCheck);

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

                var maxMillisecondsToWait = 15_000;
                var intervalMilliseconds = 500;
                var intervals = maxMillisecondsToWait / intervalMilliseconds;
                var serverReady = false;

                // wait for server to be ready to receive requests
                while (intervals-- > 0)
                {
                    serverReady = SubmitRequest(aspNetCorePort, "/alive-check");

                    if (serverReady)
                    {
                        break;
                    }

                    Thread.Sleep(intervalMilliseconds);
                }

                if (!serverReady)
                {
                    throw new Exception("Couldn't verify the application is ready to receive requests.");
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

        private bool IsNotServerLifeCheck(MockTracerAgent.Span span)
        {
            var url = SpanExpectation.GetTag(span, Tags.HttpUrl);
            if (url == null)
            {
                return true;
            }

            return !url.Contains("alive-check");
        }
    }
}
