// <copyright file="HotChocolateTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NET5_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class HotChocolate12Tests : HotChocolateTests
    {
        public HotChocolate12Tests(ITestOutputHelper output)
            : base("HotChocolate", output, nameof(HotChocolate12Tests))
        {
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.HotChocolate), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task SubmitsTraces(string packageVersion)
            => await RunSubmitsTraces(packageVersion);
    }

    [UsesVerify]
    public abstract class HotChocolateTests : TracingIntegrationTest
    {
        private const string ServiceVersion = "1.0.0";

        private readonly string _testName;

        protected HotChocolateTests(string sampleAppName, ITestOutputHelper output, string testName)
            : base(sampleAppName, output)
        {
            SetServiceVersion(ServiceVersion);

            _testName = testName;
        }

        public override Result ValidateIntegrationSpan(MockSpan span) =>
            span.Type switch
            {
                "graphql" => span.IsHotChocolate(),
                _ => Result.DefaultSuccess,
            };

        protected async Task RunSubmitsTraces(string packageVersion = "")
        {
            SetInstrumentationVerification();
            using var telemetry = this.ConfigureTelemetry();
            int? aspNetCorePort = null;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (Process process = StartSample(agent, arguments: null, packageVersion: packageVersion, aspNetCorePort: 0))
            {
                var wh = new EventWaitHandle(false, EventResetMode.AutoReset);

                using var helper = new ProcessHelper(
                    process,
                    onDataReceived: data =>
                    {
                        if (data.Contains("Now listening on:"))
                        {
                            var splitIndex = data.LastIndexOf(':');
                            aspNetCorePort = int.Parse(data.Substring(splitIndex + 1));

                            wh.Set();
                        }
                        else if (data.Contains("Unable to start Kestrel"))
                        {
                            wh.Set();
                        }

                        Output.WriteLine($"[webserver][stdout] {data}");
                    },
                    onErrorReceived: data => Output.WriteLine($"[webserver][stderr] {data}"));

                wh.WaitOne(15_000);
                if (!aspNetCorePort.HasValue)
                {
                    throw new Exception("Unable to determine port application is listening on");
                }

                Output.WriteLine($"The ASP.NET Core server is ready on port {aspNetCorePort}");

                var expectedSpans = SubmitRequests(aspNetCorePort.Value);

                if (!process.HasExited)
                {
                    // Try shutting down gracefully
                    var shutdownRequest = new RequestInfo() { HttpMethod = "GET", Url = "/shutdown" };
                    SubmitRequest(aspNetCorePort.Value, shutdownRequest);

                    WaitForProcessResult(helper);
                }

                var spans = agent.WaitForSpans(expectedSpans);
                foreach (var span in spans)
                {
                    // TODO: Refactor to use ValidateIntegrationSpans when the HotChocolate server integration is fixed. It currently produces a service name of {service]-graphql
                    var result = ValidateIntegrationSpan(span);
                    Assert.True(result.Success, result.ToString());
                }

                var settings = VerifyHelper.GetSpanVerifierSettings();

                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName("HotChocolateTests.SubmitsTraces")
                                  .DisableRequireUniquePrefix(); // all package versions should be the same

                VerifyInstrumentation(process);
            }

            telemetry.AssertIntegrationEnabled(IntegrationId.HotChocolate);
        }

        private int SubmitRequests(int aspNetCorePort)
        {
            var expectedGraphQlValidateSpanCount = 0;
            var expectedGraphQlExecuteSpanCount = 0;

            // SUCCESS: query using GET
            SubmitGraphqlRequest(url: "/graphql?query=" + WebUtility.UrlEncode("query{book{title author{name}}}"), httpMethod: "GET", graphQlRequestBody: null);

            // SUCCESS: query using POST (default)
            SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""{book{title author{name}}}""}");

            // SUCCESS: mutation
            SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: "{\"query\":\"mutation m{addBook(book:{title:\\\"New Book\\\"}){book{title}}}\"}");

            // FAILURE: query fails 'validate' step
            SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""{boook{title author{name}}}""}");

            // FAILURE: query fails 'execute' step
            SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""subscription NotImplementedSub{throwNotImplementedException{name}}""}");

            return expectedGraphQlExecuteSpanCount + expectedGraphQlValidateSpanCount;

            void SubmitGraphqlRequest(
                string url,
                string httpMethod,
                string graphQlRequestBody,
                bool failsValidation = false)
            {
                expectedGraphQlValidateSpanCount++;

                if (!failsValidation)
                {
                    expectedGraphQlExecuteSpanCount++;
                }

                SubmitRequest(
                    aspNetCorePort,
                    new RequestInfo() { Url = url, HttpMethod = httpMethod, RequestBody = graphQlRequestBody, });
            }
        }

        private void SubmitRequest(int aspNetCorePort, RequestInfo requestInfo, bool printResponseText = true)
        {
            try
            {
                var request = WebRequest.Create($"http://localhost:{aspNetCorePort}{requestInfo.Url}");
                request.Method = requestInfo.HttpMethod;

                if (requestInfo.RequestBody != null)
                {
                    byte[] requestBytes = System.Text.Encoding.UTF8.GetBytes(requestInfo.RequestBody);

                    request.ContentType = "application/json";
                    request.ContentLength = requestBytes.Length;

                    using (var dataStream = request.GetRequestStream())
                    {
                        dataStream.Write(requestBytes, 0, requestBytes.Length);
                    }
                }

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

                    if (printResponseText)
                    {
                        Output.WriteLine($"[http] {response.StatusCode} {responseText}");
                    }
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

        private class RequestInfo
        {
            public string Url { get; set; }

            public string HttpMethod { get; set; }

            public string RequestBody { get; set; }
        }
    }
}

#endif
