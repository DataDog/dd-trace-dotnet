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
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
#if NETCOREAPP3_1_OR_GREATER
    public class GraphQL7Tests : GraphQLTests
    {
        public GraphQL7Tests(ITestOutputHelper output)
            : base("GraphQL7", output, nameof(GraphQL7Tests))
        {
        }

        // Can't currently run multi-api on Windows
        public static IEnumerable<object[]> TestData =>
            EnvironmentTools.IsWindows()
                ? new[] { new object[] { string.Empty } }
                : PackageVersions.GraphQL7;

        [SkippableTheory]
        [MemberData(nameof(TestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTraces(string packageVersion)
            => await RunSubmitsTraces(packageVersion, true);
    }

    public class GraphQL4Tests : GraphQLTests
    {
        public GraphQL4Tests(ITestOutputHelper output)
            : base("GraphQL4", output, nameof(GraphQL4Tests))
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
        public async Task SubmitsTraces(string packageVersion)
            => await RunSubmitsTraces(packageVersion);
    }
#endif

    public class GraphQL3Tests : GraphQLTests
    {
        public GraphQL3Tests(ITestOutputHelper output)
            : base("GraphQL3", output, nameof(GraphQL3Tests))
        {
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task SubmitsTraces()
            => await RunSubmitsTraces();
    }

    public class GraphQL2Tests : GraphQLTests
    {
        public GraphQL2Tests(ITestOutputHelper output)
            : base("GraphQL", output, nameof(GraphQL2Tests))
        {
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task SubmitsTraces()
        {
            if (EnvironmentTools.IsWindows()
             && !EnvironmentHelper.IsCoreClr()
             && EnvironmentTools.IsTestTarget64BitProcess())
            {
                throw new SkipException("ASP.NET Core running on .NET Framework requires x86, because it uses " +
                                        "the x86 version of libuv unless you compile the dll _explicitly_ for x64, " +
                                        "which we don't do any more");
            }

            await RunSubmitsTraces();
        }
    }

    [UsesVerify]
    public abstract class GraphQLTests : TracingIntegrationTest
    {
        private const string ServiceVersion = "1.0.0";

        private readonly string _testName;

        protected GraphQLTests(string sampleAppName, ITestOutputHelper output, string testName)
            : base(sampleAppName, output)
        {
            SetServiceVersion(ServiceVersion);

            _testName = testName;
        }

        public override Result ValidateIntegrationSpan(MockSpan span) =>
            span.Type switch
            {
                "graphql" => span.IsGraphQL(),
                _ => Result.DefaultSuccess,
            };

        protected async Task RunSubmitsTraces(string packageVersion = "", bool usingWebsockets = false)
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

                var expectedSpans = SubmitRequests(aspNetCorePort.Value, usingWebsockets);

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
                    // TODO: Refactor to use ValidateIntegrationSpans when the graphql server integration is fixed. It currently produces a service name of {service]-graphql
                    var result = ValidateIntegrationSpan(span);
                    Assert.True(result.Success, result.ToString());
                }

                var settings = VerifyHelper.GetSpanVerifierSettings();

                // hacky scrubber for the fact that version 4.1.0+ switched to using " in error message in one place
                // where every other version uses '
                settings.AddSimpleScrubber("Did you mean \"appearsIn\"", "Did you mean 'appearsIn'");
                // Graphql 5 has different error message for missing subscription
                settings.AddSimpleScrubber("Could not resolve source stream for field", "Error trying to resolve field");

                // Overriding the type name here as we have multiple test classes in the file
                // Ensures that we get nice file nesting in Solution Explorer
                var fxSuffix = EnvironmentHelper.IsCoreClr() ? string.Empty : ".netfx";
                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName($"{_testName}.SubmitsTraces{fxSuffix}")
                                  .DisableRequireUniquePrefix(); // all package versions should be the same

                VerifyInstrumentation(process);
            }

            telemetry.AssertIntegrationEnabled(IntegrationId.GraphQL);
        }

        private int SubmitRequests(int aspNetCorePort, bool usingWebsockets)
        {
            var expectedGraphQlValidateSpanCount = 0;
            var expectedGraphQlExecuteSpanCount = 0;

            SubmitHttpRequests();

            if (usingWebsockets)
            {
                SubmitWebsocketRequests();
            }

            return expectedGraphQlExecuteSpanCount + expectedGraphQlValidateSpanCount;

            void SubmitHttpRequests()
            {
                // SUCCESS: query using GET
                SubmitGraphqlRequest(url: "/graphql?query=" + WebUtility.UrlEncode("query{hero{name appearsIn}}"), httpMethod: "GET", graphQlRequestBody: null);

                // SUCCESS: query using POST (default)
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""query HeroQuery{hero {name appearsIn}}"",""operationName"": ""HeroQuery""}");

                // SUCCESS: mutation
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""mutation AddBobaFett($human:HumanInput!){createHuman(human: $human){id name}}"",""variables"":{""human"":{""name"": ""Boba Fett""}}}");

                // SUCCESS: subscription
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{ ""query"":""subscription HumanAddedSub{humanAdded{name}}""}");

                // TODO: When parse is implemented, add a test that fails 'parse' step

                // FAILURE: query fails 'validate' step
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""query HumanError{human(id:1){name apearsIn}}""}", failsValidation: true);

                // FAILURE: query fails 'execute' step
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""subscription NotImplementedSub{throwNotImplementedException{name}}""}");

                // TODO: When parse is implemented, add a test that fails 'resolve' step
            }

            void SubmitWebsocketRequests()
            {
                // SUCCESS: query using Websocket
                SubmitGraphqlRequest(url: "/graphql", httpMethod: null, graphQlRequestBody: @"{""type"": ""start"",""id"": ""1"",""payload"": {""query"": ""query HeroQuery{hero {name appearsIn}}"",""variables"": {},}}", false, true);

                // SUCCESS: mutation using Websocket
                SubmitGraphqlRequest(url: "/graphql", httpMethod: null, graphQlRequestBody: @"{""type"": ""start"",""id"": ""1"",""payload"": {""query"": ""mutation AddBobaFett($human:HumanInput!){createHuman(human: $human){id name}}"",""variables"": {""human"":{""name"": ""Boba Fett""}},}}", false, true);

                // FAILURE: query fails 'validate' step using Websocket
                SubmitGraphqlRequest(url: "/graphql", httpMethod: null, graphQlRequestBody: @"{""type"": ""start"",""id"": ""1"",""payload"": {""query"": ""query HumanError{human(id:1){name apearsIn}}"",""variables"": {},}}", true, true);

                // FAILURE: query fails 'execute' step using Websocket
                SubmitGraphqlRequest(url: "/graphql", httpMethod: null, graphQlRequestBody: @"{""type"": ""start"",""id"": ""1"",""payload"": {""query"": ""subscription NotImplementedSub{throwNotImplementedException {name}}"",""variables"": {},}}", false, true);
            }

            void SubmitGraphqlRequest(
                string url,
                string httpMethod,
                string graphQlRequestBody,
                bool failsValidation = false,
                bool isWebsocket = false)
            {
                expectedGraphQlValidateSpanCount++;

                if (!failsValidation)
                {
                    expectedGraphQlExecuteSpanCount++;
                }

                var requestInfo =
                    new RequestInfo() { Url = url, HttpMethod = httpMethod, RequestBody = graphQlRequestBody, };

                if (isWebsocket)
                {
                    SubmitWebsocketRequest(aspNetCorePort, requestInfo);
                }
                else
                {
                    SubmitRequest(aspNetCorePort, requestInfo);
                }
            }
        }

        private void SubmitRequest(int aspNetCorePort, RequestInfo requestInfo, bool printResponseText = true)
        {
            try
            {
                var request = WebRequest.Create($"http://localhost:{aspNetCorePort}{requestInfo.Url}");
                request.Method = requestInfo.HttpMethod;
                ((HttpWebRequest)request).UserAgent = "testhelper";

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

        private void SubmitWebsocketRequest(int aspNetCorePort, RequestInfo requestInfo)
        {
            var uri = new Uri($"ws://localhost:{aspNetCorePort}{requestInfo.Url}");
            var webSocket = new ClientWebSocket();
            webSocket.Options.AddSubProtocol("graphql-ws");

            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(5)); // 5 seconds timeout

            try
            {
                webSocket.ConnectAsync(uri, cancellationTokenSource.Token).Wait();
                Output.WriteLine("[websocket] WebSocket connection established");

                // GraphQL First packet initialization
                const string initPayload = @"{
                    ""type"": ""connection_init"",
                    ""payload"": {""payload"": {""Accept"":""application/json""}}
                }";
                var initBuffer = System.Text.Encoding.UTF8.GetBytes(initPayload);
                var initSegment = new ArraySegment<byte>(initBuffer);
                webSocket.SendAsync(initSegment, WebSocketMessageType.Text, true, cancellationTokenSource.Token).Wait();
                Output.WriteLine("[websocket] Connection initialized");

                // Send test request
                var buffer = System.Text.Encoding.UTF8.GetBytes(requestInfo.RequestBody);
                var segment = new ArraySegment<byte>(buffer);
                webSocket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationTokenSource.Token).Wait();
                Output.WriteLine("[websocket] Request sent");
            }
            catch (Exception ex)
            {
                Output.WriteLine($"[websocket] WebSocket connection error: {ex.Message}");
            }
            finally
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationTokenSource.Token).Wait();
                    Output.WriteLine("[websocket] WebSocket connection closed");
                }

                webSocket.Dispose();
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
