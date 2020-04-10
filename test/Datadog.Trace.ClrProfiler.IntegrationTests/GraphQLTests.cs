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

#if !NET452

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class GraphQLTests : TestHelper
    {
        private static readonly string _graphQLValidateOperationName = "graphql.validate";
        private static readonly string _graphQLExecuteOperationName = "graphql.execute";

        private static readonly List<RequestInfo> _requests;
        private static readonly List<WebServerSpanExpectation> _expectations;
        private static int _expectedGraphQLValidateSpanCount;
        private static int _expectedGraphQLExecuteSpanCount;

        static GraphQLTests()
        {
            _requests = new List<RequestInfo>(0);
            _expectations = new List<WebServerSpanExpectation>();
            _expectedGraphQLValidateSpanCount = 0;
            _expectedGraphQLExecuteSpanCount = 0;

            // SUCCESS: query using GET
            CreateGraphQLRequestsAndExpectations(url: "/graphql?query=" + WebUtility.UrlEncode("query{hero{name appearsIn}}"), httpMethod: "GET", resourceName: "Query operation", graphQLRequestBody: null, graphQLOperationType: "Query", graphQLOperationName: null, graphQLSource: "query{hero{name appearsIn} }");

            // SUCCESS: query using POST (default)
            CreateGraphQLRequestsAndExpectations(url: "/graphql", httpMethod: "POST", resourceName: "Query HeroQuery", graphQLRequestBody: @"{""query"":""query HeroQuery{hero {name appearsIn}}"",""operationName"": ""HeroQuery""}", graphQLOperationType: "Query", graphQLOperationName: "HeroQuery", graphQLSource: "query HeroQuery{hero{name appearsIn}}");

            // SUCCESS: mutation
            CreateGraphQLRequestsAndExpectations(url: "/graphql", httpMethod: "POST", resourceName: "Mutation AddBobaFett", graphQLRequestBody: @"{""query"":""mutation AddBobaFett($human:HumanInput!){createHuman(human: $human){id name}}"",""variables"":{""human"":{""name"": ""Boba Fett""}}}", graphQLOperationType: "Mutation", graphQLOperationName: "AddBobaFett", graphQLSource: "mutation AddBobaFett($human:HumanInput!){createHuman(human: $human){id name}}");

            // SUCCESS: subscription
            CreateGraphQLRequestsAndExpectations(url: "/graphql", httpMethod: "POST", resourceName: "Subscription HumanAddedSub", graphQLRequestBody: @"{ ""query"":""subscription HumanAddedSub{humanAdded{name}}""}", graphQLOperationType: "Subscription", graphQLOperationName: "HumanAddedSub", graphQLSource: "subscription HumanAddedSub{humanAdded{name}}");

            // TODO: When parse is implemented, add a test that fails 'parse' step

            // FAILURE: query fails 'validate' step
            CreateGraphQLRequestsAndExpectations(url: "/graphql", httpMethod: "POST", resourceName: "Query HumanError", graphQLRequestBody: @"{""query"":""query HumanError{human(id:1){name apearsIn}}""}", graphQLOperationType: "Query", graphQLOperationName: null, failsValidation: true, graphQLSource: "query HumanError{human(id:1){name apearsIn}}");

            // FAILURE: query fails 'execute' step
            CreateGraphQLRequestsAndExpectations(url: "/graphql", httpMethod: "POST", resourceName: "Subscription NotImplementedSub", graphQLRequestBody: @"{""query"":""subscription NotImplementedSub{throwNotImplementedException{name}}""}", graphQLOperationType: "Subscription", graphQLOperationName: "NotImplementedSub", graphQLSource: "subscription NotImplementedSub{throwNotImplementedException{name}}", failsExecution: true);

            // TODO: When parse is implemented, add a test that fails 'resolve' step
        }

        public GraphQLTests(ITestOutputHelper output)
            : base("GraphQL", output)
        {
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void SubmitsTraces()
        {
            int agentPort = TcpPortProvider.GetOpenPort();
            int aspNetCorePort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (Process process = StartSample(agent.Port, arguments: null, packageVersion: string.Empty, aspNetCorePort: aspNetCorePort))
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

                var maxMillisecondsToWait = 15_000;
                var intervalMilliseconds = 500;
                var intervals = maxMillisecondsToWait / intervalMilliseconds;
                var serverReady = false;

                // wait for server to be ready to receive requests
                while (intervals-- > 0)
                {
                    var aliveCheckRequest = new RequestInfo() { HttpMethod = "GET", Url = "/alive-check" };
                    try
                    {
                        serverReady = SubmitRequest(aspNetCorePort, aliveCheckRequest, false) == HttpStatusCode.OK;
                    }
                    catch
                    {
                        // ignore
                    }

                    if (serverReady)
                    {
                        Output.WriteLine("The server is ready.");
                        break;
                    }

                    Thread.Sleep(intervalMilliseconds);
                }

                if (!serverReady)
                {
                    throw new Exception("Couldn't verify the application is ready to receive requests.");
                }

                var testStart = DateTime.Now;

                SubmitRequests(aspNetCorePort);
                var graphQLValidateSpans = agent.WaitForSpans(_expectedGraphQLValidateSpanCount, operationName: _graphQLValidateOperationName, returnAllOperations: false)
                                 .GroupBy(s => s.SpanId)
                                 .Select(grp => grp.First())
                                 .OrderBy(s => s.Start);
                var graphQLExecuteSpans = agent.WaitForSpans(_expectedGraphQLExecuteSpanCount, operationName: _graphQLExecuteOperationName, returnAllOperations: false)
                                 .GroupBy(s => s.SpanId)
                                 .Select(grp => grp.First())
                                 .OrderBy(s => s.Start);

                if (!process.HasExited)
                {
                    process.Kill();
                }

                var spans = graphQLValidateSpans.Concat(graphQLExecuteSpans).ToList();
                SpanTestHelpers.AssertExpectationsMet(_expectations, spans);
            }
        }

        private static void CreateGraphQLRequestsAndExpectations(
            string url,
            string httpMethod,
            string resourceName,
            string graphQLRequestBody,
            string graphQLOperationType,
            string graphQLOperationName,
            string graphQLSource,
            bool failsValidation = false,
            bool failsExecution = false)
        {
            _requests.Add(new RequestInfo()
            {
                Url = url,
                HttpMethod = httpMethod,
                RequestBody = graphQLRequestBody,
            });

            // Expect a 'validate' span
            _expectations.Add(new GraphQLSpanExpectation("Samples.GraphQL-graphql", _graphQLValidateOperationName, _graphQLValidateOperationName)
            {
                OriginalUri = url,
                GraphQLRequestBody = graphQLRequestBody,
                GraphQLOperationType = null,
                GraphQLOperationName = graphQLOperationName,
                GraphQLSource = graphQLSource,
                IsGraphQLError = failsValidation,
            });
            _expectedGraphQLValidateSpanCount++;

            if (failsValidation) { return; }

            // Expect an 'execute' span
            _expectations.Add(new GraphQLSpanExpectation("Samples.GraphQL-graphql", _graphQLExecuteOperationName, resourceName)
            {
                OriginalUri = url,
                GraphQLRequestBody = graphQLRequestBody,
                GraphQLOperationType = graphQLOperationType,
                GraphQLOperationName = graphQLOperationName,
                GraphQLSource = graphQLSource,
                IsGraphQLError = failsExecution,
            });
            _expectedGraphQLExecuteSpanCount++;

            if (failsExecution) { return; }
        }

        private void SubmitRequests(int aspNetCorePort)
        {
            foreach (RequestInfo requestInfo in _requests)
            {
                SubmitRequest(aspNetCorePort, requestInfo);
            }
        }

        private HttpStatusCode SubmitRequest(int aspNetCorePort, RequestInfo requestInfo, bool printResponseText = true)
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

                    return response.StatusCode;
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

                    return response.StatusCode;
                }
            }

            return HttpStatusCode.BadRequest;
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
