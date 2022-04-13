// <copyright file="GraphQLTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
#if NETCOREAPP3_1_OR_GREATER
    public class GraphQL4Tests : GraphQLTests
    {
        public GraphQL4Tests(ITestOutputHelper output)
            : base("GraphQL4", output)
        {
        }

        // Can't currently run multi-api on Windows
        public static IEnumerable<object[]> TestData =>
            EnvironmentTools.IsWindows()
                ? new[] { new object[] { string.Empty } }
                : PackageVersions.GraphQL;

        [SkippableTheory]
        [MemberData(nameof(TestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public Task SubmitsTraces(string packageVersion)
            => RunSubmitsTraces(packageVersion);
    }
#endif

    public class GraphQL3Tests : GraphQLTests
    {
        public GraphQL3Tests(ITestOutputHelper output)
            : base("GraphQL3", output)
        {
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public Task SubmitsTraces()
            => RunSubmitsTraces();
    }

    public class GraphQL2Tests : GraphQLTests
    {
        public GraphQL2Tests(ITestOutputHelper output)
            : base("GraphQL", output)
        {
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public Task SubmitsTraces()
            => RunSubmitsTraces();
    }

    public abstract class GraphQLTests : TestHelper
    {
        private const string ServiceVersion = "1.0.0";

        private static readonly string _graphQLValidateOperationName = "graphql.validate";
        private static readonly string _graphQLExecuteOperationName = "graphql.execute";

        private List<RequestInfo> _requests;
        private List<WebServerSpanExpectation> _expectations;
        private int _expectedGraphQLValidateSpanCount;
        private int _expectedGraphQLExecuteSpanCount;

        protected GraphQLTests(string sampleAppName, ITestOutputHelper output)
            : base(sampleAppName, output)
        {
            InitializeExpectations(sampleAppName);
            SetServiceVersion(ServiceVersion);
        }

        protected async Task RunSubmitsTraces(string packageVersion = "")
        {
            using var telemetry = this.ConfigureTelemetry();
            int? aspNetCorePort = null;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (Process process = StartSample(agent, arguments: null, packageVersion: packageVersion, aspNetCorePort: 0))
            {
                var wh = new EventWaitHandle(false, EventResetMode.AutoReset);

                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        if (args.Data.Contains("Now listening on:"))
                        {
                            var splitIndex = args.Data.LastIndexOf(':');
                            aspNetCorePort = int.Parse(args.Data.Substring(splitIndex + 1));

                            wh.Set();
                        }
                        else if (args.Data.Contains("Unable to start Kestrel"))
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

                wh.WaitOne(15_000);
                if (!aspNetCorePort.HasValue)
                {
                    throw new Exception("Unable to determine port application is listening on");
                }

                Output.WriteLine($"The ASP.NET Core server is ready on port {aspNetCorePort}");

                SubmitRequests(aspNetCorePort.Value);
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
                    // Try shutting down gracefully
                    var shutdownRequest = new RequestInfo() { HttpMethod = "GET", Url = "/shutdown" };
                    SubmitRequest(aspNetCorePort.Value, shutdownRequest);

                    if (!process.WaitForExit(5000))
                    {
                        Output.WriteLine("The process didn't exit in time. Taking proc dump and killing it.");
                        await TakeMemoryDump(process);

                        process.Kill();
                    }
                }

                var spans = graphQLValidateSpans.Concat(graphQLExecuteSpans).ToList();
                SpanTestHelpers.AssertExpectationsMet(_expectations, spans);
            }

            telemetry.AssertIntegrationEnabled(IntegrationId.GraphQL);
        }

        private void InitializeExpectations(string sampleName)
        {
            _requests = new List<RequestInfo>(0);
            _expectations = new List<WebServerSpanExpectation>();
            _expectedGraphQLValidateSpanCount = 0;
            _expectedGraphQLExecuteSpanCount = 0;

            // SUCCESS: query using GET
            CreateGraphQLRequestsAndExpectations(sampleName, url: "/graphql?query=" + WebUtility.UrlEncode("query{hero{name appearsIn}}"), httpMethod: "GET", resourceName: "Query operation", graphQLRequestBody: null, graphQLOperationType: "Query", graphQLOperationName: null, graphQLSource: "query{hero{name appearsIn} }");

            // SUCCESS: query using POST (default)
            CreateGraphQLRequestsAndExpectations(sampleName, url: "/graphql", httpMethod: "POST", resourceName: "Query HeroQuery", graphQLRequestBody: @"{""query"":""query HeroQuery{hero {name appearsIn}}"",""operationName"": ""HeroQuery""}", graphQLOperationType: "Query", graphQLOperationName: "HeroQuery", graphQLSource: "query HeroQuery{hero{name appearsIn}}");

            // SUCCESS: mutation
            CreateGraphQLRequestsAndExpectations(sampleName, url: "/graphql", httpMethod: "POST", resourceName: "Mutation AddBobaFett", graphQLRequestBody: @"{""query"":""mutation AddBobaFett($human:HumanInput!){createHuman(human: $human){id name}}"",""variables"":{""human"":{""name"": ""Boba Fett""}}}", graphQLOperationType: "Mutation", graphQLOperationName: "AddBobaFett", graphQLSource: "mutation AddBobaFett($human:HumanInput!){createHuman(human: $human){id name}}");

            // SUCCESS: subscription
            CreateGraphQLRequestsAndExpectations(sampleName, url: "/graphql", httpMethod: "POST", resourceName: "Subscription HumanAddedSub", graphQLRequestBody: @"{ ""query"":""subscription HumanAddedSub{humanAdded{name}}""}", graphQLOperationType: "Subscription", graphQLOperationName: "HumanAddedSub", graphQLSource: "subscription HumanAddedSub{humanAdded{name}}");

            // TODO: When parse is implemented, add a test that fails 'parse' step

            // FAILURE: query fails 'validate' step
            CreateGraphQLRequestsAndExpectations(sampleName, url: "/graphql", httpMethod: "POST", resourceName: "Query HumanError", graphQLRequestBody: @"{""query"":""query HumanError{human(id:1){name apearsIn}}""}", graphQLOperationType: "Query", graphQLOperationName: null, failsValidation: true, graphQLSource: "query HumanError{human(id:1){name apearsIn}}");

            // FAILURE: query fails 'execute' step
            CreateGraphQLRequestsAndExpectations(sampleName, url: "/graphql", httpMethod: "POST", resourceName: "Subscription NotImplementedSub", graphQLRequestBody: @"{""query"":""subscription NotImplementedSub{throwNotImplementedException{name}}""}", graphQLOperationType: "Subscription", graphQLOperationName: "NotImplementedSub", graphQLSource: "subscription NotImplementedSub{throwNotImplementedException{name}}", failsExecution: true);

            // TODO: When parse is implemented, add a test that fails 'resolve' step
        }

        private void CreateGraphQLRequestsAndExpectations(
            string sampleName,
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
            _expectations.Add(new GraphQLSpanExpectation($"Samples.{sampleName}-graphql", _graphQLValidateOperationName, _graphQLValidateOperationName)
            {
                OriginalUri = url,
                GraphQLRequestBody = graphQLRequestBody,
                GraphQLOperationType = null,
                GraphQLOperationName = graphQLOperationName,
                GraphQLSource = graphQLSource,
                IsGraphQLError = failsValidation,
                ServiceVersion = null
            });
            _expectedGraphQLValidateSpanCount++;

            if (failsValidation) { return; }

            // Expect an 'execute' span
            _expectations.Add(new GraphQLSpanExpectation($"Samples.{sampleName}-graphql", _graphQLExecuteOperationName, resourceName)
            {
                OriginalUri = url,
                GraphQLRequestBody = graphQLRequestBody,
                GraphQLOperationType = graphQLOperationType,
                GraphQLOperationName = graphQLOperationName,
                GraphQLSource = graphQLSource,
                IsGraphQLError = failsExecution,
                ServiceVersion = null
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
