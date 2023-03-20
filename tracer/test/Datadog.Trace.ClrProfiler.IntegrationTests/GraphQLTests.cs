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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
#if NETCOREAPP3_1_OR_GREATER
    public class GraphQL7Tests : GraphQLTests
    {
        public GraphQL7Tests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base("GraphQL7", fixture, output, nameof(GraphQL7Tests))
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
            => await RunSubmitsTraces(packageVersion: packageVersion);

        [SkippableTheory]
        [MemberData(nameof(TestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTracesWebsockets(string packageVersion)
            => await RunSubmitsTraces("SubmitsTracesWebsockets", packageVersion, true);
    }

    public class GraphQL4Tests : GraphQLTests
    {
        public GraphQL4Tests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base("GraphQL4", fixture, output, nameof(GraphQL4Tests))
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
            => await RunSubmitsTraces(packageVersion: packageVersion);

        [SkippableTheory]
        [MemberData(nameof(TestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTracesWebsockets(string packageVersion)
            => await RunSubmitsTraces("SubmitsTracesWebsockets", packageVersion, true);
    }
#endif

    public class GraphQL3Tests : GraphQLTests
    {
        public GraphQL3Tests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base("GraphQL3", fixture, output, nameof(GraphQL3Tests))
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
        public GraphQL2Tests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base("GraphQL", fixture, output, nameof(GraphQL2Tests))
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
    public abstract class GraphQLTests : TracingIntegrationTest, IClassFixture<AspNetCoreTestFixture>
    {
        private const string ServiceVersion = "1.0.0";

        private readonly string _testName;

        protected GraphQLTests(string sampleAppName, AspNetCoreTestFixture fixture, ITestOutputHelper output, string testName)
            : base(sampleAppName, output)
        {
            SetServiceVersion(ServiceVersion);

            _testName = testName;

            Fixture = fixture;
            Fixture.SetOutput(output);
        }

        protected AspNetCoreTestFixture Fixture { get; }

        public override void Dispose()
        {
            Fixture.SetOutput(null);
        }

        public override Result ValidateIntegrationSpan(MockSpan span) =>
            span.Type switch
            {
                "graphql" => span.IsGraphQL(),
                _ => Result.DefaultSuccess,
            };

        protected async Task RunSubmitsTraces(string testName = "SubmitsTraces", string packageVersion = "", bool usingWebsockets = false)
        {
            SetInstrumentationVerification();

            await Fixture.TryStartApp(this);
            var testStart = DateTime.UtcNow;
            var expectedSpans = await SubmitRequests(Fixture.HttpPort, usingWebsockets);

            var spans = Fixture.Agent.WaitForSpans(count: expectedSpans, minDateTime: testStart, returnAllOperations: true);
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
                              .UseFileName($"{_testName}.{testName}{fxSuffix}")
                              .DisableRequireUniquePrefix(); // all package versions should be the same

            VerifyInstrumentation(Fixture.Process);
        }

        private async Task<int> SubmitRequests(int aspNetCorePort, bool usingWebsockets)
        {
            var expectedGraphQlValidateSpanCount = 0;
            var expectedGraphQlExecuteSpanCount = 0;
            var expectedAspNetcoreRequestSpanCount = 0;

            if (usingWebsockets)
            {
                await SubmitWebsocketRequests();
            }
            else
            {
                var isV7 = _testName.Contains("7");
                SubmitHttpRequests(isV7);
            }

            Output.WriteLine($"[SPANS] Expected graphql.execute Spans: {expectedGraphQlExecuteSpanCount}");
            Output.WriteLine($"[SPANS] Expected graphql.validate Spans: {expectedGraphQlValidateSpanCount}");
            Output.WriteLine($"[SPANS] Expected aspnet_core.request Spans: {expectedAspNetcoreRequestSpanCount}");
            Output.WriteLine($"[SPANS] Total Spans number: {(expectedGraphQlExecuteSpanCount + expectedGraphQlValidateSpanCount + expectedAspNetcoreRequestSpanCount)}");
            return expectedGraphQlExecuteSpanCount + expectedGraphQlValidateSpanCount + expectedAspNetcoreRequestSpanCount;

            void SubmitHttpRequests(bool isV7)
            {
                // SUCCESS: query using GET
                SubmitGraphqlRequest(url: "/graphql?query=" + WebUtility.UrlEncode("query{hero{name appearsIn}}"), httpMethod: "GET", graphQlRequestBody: null);

                // SUCCESS: query using POST (default)
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""query HeroQuery{hero {name appearsIn}}"",""operationName"": ""HeroQuery""}");

                // SUCCESS: mutation
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""mutation AddBobaFett($human:HumanInput!){createHuman(human: $human){id name}}"",""variables"":{""human"":{""name"": ""Boba Fett""}}}");

                // SUCCESS: subscription or FAILURE: fails 'validate' (can't do POST for subscription on v7+)
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{ ""query"":""subscription HumanAddedSub{humanAdded{name}}""}", failsValidation: isV7);

                // TODO: When parse is implemented, add a test that fails 'parse' step

                // FAILURE: query fails 'validate' step
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""query HumanError{human(id:1){name apearsIn}}""}", failsValidation: true);

                // FAILURE: query fails 'execute' step but fails at 'validate' on v7
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""subscription NotImplementedSub{throwNotImplementedException{name}}""}", failsValidation: isV7);

                // TODO: When parse is implemented, add a test that fails 'resolve' step
            }

            async Task SubmitWebsocketRequests()
            {
                // SUCCESS: query using Websocket
                await SubmitGraphqlWebsocketRequest(url: "/graphql", httpMethod: null, graphQlRequestBody: @"{""type"": ""start"",""id"": ""1"",""payload"": {""query"": ""query HeroQuery{hero {name appearsIn}}"",""variables"": {},}}", false);

                // FAILURE: query fails 'execute' step using Websocket
                await SubmitGraphqlWebsocketRequest(url: "/graphql", httpMethod: null, graphQlRequestBody: @"{""type"": ""start"",""id"": ""1"",""payload"": {""query"": ""subscription NotImplementedSub{throwNotImplementedException {name}}"",""variables"": {},}}", false);

                // FAILURE: query fails 'validate' step using Websocket
                await SubmitGraphqlWebsocketRequest(url: "/graphql", httpMethod: null, graphQlRequestBody: @"{""type"": ""start"",""id"": ""1"",""payload"": {""query"": ""query HumanError{human(id:1){name apearsIn}}"",""variables"": {},}}", true);

                // SUCCESS: mutation using Websocket
                await SubmitGraphqlWebsocketRequest(url: "/graphql", httpMethod: null, graphQlRequestBody: @"{""type"": ""start"",""id"": ""1"",""payload"": {""query"": ""mutation AddBobaFett($human:HumanInput!){createHuman(human: $human){id name}}"",""variables"": {""human"":{""name"": ""Boba Fett""}},}}", false);
            }

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

                expectedAspNetcoreRequestSpanCount++;
                SubmitRequest(
                    aspNetCorePort,
                    new RequestInfo() { Url = url, HttpMethod = httpMethod, RequestBody = graphQlRequestBody, });
            }

            async Task SubmitGraphqlWebsocketRequest(
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

                expectedAspNetcoreRequestSpanCount++;
                await SubmitWebsocketRequest(
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

        private async Task SubmitWebsocketRequest(int aspNetCorePort, RequestInfo requestInfo)
        {
            var uri = new Uri($"ws://localhost:{aspNetCorePort}{requestInfo.Url}");
            var webSocket = new ClientWebSocket();
            webSocket.Options.AddSubProtocol("graphql-ws");

            try
            {
                await webSocket.ConnectAsync(uri, CancellationToken.None);
                Output.WriteLine("[websocket] WebSocket connection established");

                // GraphQL First packet initialization
                const string initPayload = @"{
                    ""type"": ""connection_init"",
                    ""payload"": {""Accept"":""application/json""}
                }";
                var initBuffer = System.Text.Encoding.UTF8.GetBytes(initPayload);
                var initSegment = new ArraySegment<byte>(initBuffer);
                await webSocket.SendAsync(initSegment, WebSocketMessageType.Text, true, CancellationToken.None);
                Output.WriteLine("[websocket] Connection initialized (init packet sent) 1/2");
                await WaitForMessage();
                Output.WriteLine("[websocket] Connection initialized (init packet received) 2/2");

                // Send test request
                Output.WriteLine($"[websocket] Send request: {requestInfo.RequestBody}");
                var buffer = System.Text.Encoding.UTF8.GetBytes(requestInfo.RequestBody);
                var segment = new ArraySegment<byte>(buffer);
                await webSocket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                Output.WriteLine("[websocket] Request sent");
                Output.WriteLine("[websocket] Waiting for the response:");
                await WaitForMessage();
                Output.WriteLine("[websocket] Response received.");

                // Terminate the GraphQl Websocket connection
                Output.WriteLine("[websocket] Send connection_terminate...");
                const string terminatePayload = @"{
                    ""type"": ""connection_terminate"",
                    ""payload"": {}
                }";
                var terminateBuffer = System.Text.Encoding.UTF8.GetBytes(terminatePayload);
                var terminateSegment = new ArraySegment<byte>(terminateBuffer);
                await webSocket.SendAsync(terminateSegment, WebSocketMessageType.Text, true, CancellationToken.None);
                Output.WriteLine("[websocket] connection_terminate sent");
            }
            catch (Exception ex)
            {
                Output.WriteLine($"[websocket] WebSocket connection error: {ex.Message}");
            }
            finally
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    Output.WriteLine("[websocket] WebSocket connection closed");
                }

                webSocket.Dispose();
                Output.WriteLine("[websocket] Websocket connection disposed.");
            }

            async Task WaitForMessage()
            {
                Output.WriteLine("[websocket][debug] Start waiting for a message!");
                var ms = new MemoryStream();
                while (webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult res;
                    do
                    {
                        var messageBuffer = WebSocket.CreateClientBuffer(1024, 16);
                        res = await webSocket.ReceiveAsync(messageBuffer, CancellationToken.None);
                        ms.Write(messageBuffer.Array, messageBuffer.Offset, res.Count);
                    }
                    while (!res.EndOfMessage);

                    // Message received from the websocket
                    var msgString = Encoding.UTF8.GetString(ms.ToArray());
                    ms = new MemoryStream(); // Reset Stream

                    if (msgString.Length == 0)
                    {
                        Output.WriteLine("[websocket][debug] Received an empty message.");
                        continue;
                    }

                    // Check if the data received is a 'complete' message
                    Output.WriteLine("[websocket] before json: " + msgString);
                    var jsonObj = JObject.Parse(msgString);
                    var typeData = jsonObj.GetValue("type")?.Value<string>();
                    if (typeData is "complete" or "error" or "connection_ack")
                    {
                        Output.WriteLine("[websocket][debug] Complete or Error or Connection_ack Message");
                        break;
                    }

                    if (res.MessageType == WebSocketMessageType.Close)
                    {
                        Output.WriteLine("[websocket][debug] Close websocket");
                        break;
                    }
                }

                Output.WriteLine("[websocket] Stopped waiting for a message");
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
