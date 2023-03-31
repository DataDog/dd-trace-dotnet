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
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Newtonsoft.Json.Linq;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class HotChocolate13Tests : HotChocolateTests
    {
        public HotChocolate13Tests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base("HotChocolate", fixture, output, nameof(HotChocolate13Tests))
        {
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.HotChocolate), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task SubmitsTracesHttp(string packageVersion)
            => await RunSubmitsTraces(packageVersion);

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.HotChocolate), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task SubmitsTracesWebsockets(string packageVersion)
            => await RunSubmitsTraces(packageVersion, true);
    }

    [UsesVerify]
    public abstract class HotChocolateTests : TracingIntegrationTest, IClassFixture<AspNetCoreTestFixture>
    {
        private const string ServiceVersion = "1.0.0";

        private readonly string _testName;

        protected HotChocolateTests(string sampleAppName, AspNetCoreTestFixture fixture, ITestOutputHelper output, string testName)
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
                "graphql" => span.IsHotChocolate(),
                _ => Result.DefaultSuccess,
            };

        protected async Task RunSubmitsTraces(string packageVersion = "", bool usingWebsockets = false)
        {
            SetInstrumentationVerification();

            await Fixture.TryStartApp(this);
            var testStart = DateTime.UtcNow;
            var expectedSpans = await SubmitRequests(Fixture.HttpPort, usingWebsockets);

            var spans = Fixture.Agent.WaitForSpans(count: expectedSpans, minDateTime: testStart, returnAllOperations: true);
            foreach (var span in spans)
            {
                // TODO: Refactor to use ValidateIntegrationSpans when the HotChocolate server integration is fixed. It currently produces a service name of {service]-graphql
                var result = ValidateIntegrationSpan(span);
                Assert.True(result.Success, result.ToString());
            }

            var settings = VerifyHelper.GetSpanVerifierSettings();

            await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName($"HotChocolateTests{(usingWebsockets ? "Websockets" : string.Empty)}.SubmitsTraces")
                                  .DisableRequireUniquePrefix(); // all package versions should be the same

            VerifyInstrumentation(Fixture.Process);
        }

        private async Task<int> SubmitRequests(int aspNetCorePort, bool usingWebsockets)
        {
            var expectedGraphQlExecuteSpanCount = 0;
            var expectedAspNetcoreRequestSpanCount = 0;

            if (usingWebsockets)
            {
                await SubmitWebsocketRequests();
            }
            else
            {
                SubmitHttpRequests();
            }

            void SubmitHttpRequests()
            {
                // SUCCESS: query using GET
                SubmitGraphqlRequest(url: "/graphql?query=" + WebUtility.UrlEncode("query{book{title author{name}}}"), httpMethod: "GET", graphQlRequestBody: null);

                // SUCCESS: query using POST (default)
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""query Book{book{title author{name}}}""}");

                // SUCCESS: mutation
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""mutation m{addBook(book:{title:\""New Book\""}){book{title}}}""}");

                // FAILURE: query fails 'validate' step
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""{boook{title author{name}}}""}");

                // FAILURE: query fails 'execute' step
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""subscription NotImplementedSub{throwNotImplementedException{name}}""}");
            }

            async Task SubmitWebsocketRequests()
            {
                // SUCCESS: query using Websocket
                await SubmitGraphqlWebsocketRequest(url: "/graphql", httpMethod: null, graphQlRequestBody: @"{""type"": ""start"",""id"": ""1"",""payload"": {""query"": ""query Book{book{title author{name}}}"",""variables"": {}}}");

                // SUCCESS: mutation using Websocket
                await SubmitGraphqlWebsocketRequest(url: "/graphql", httpMethod: null, graphQlRequestBody: @"{""type"": ""start"",""id"": ""1"",""payload"": {""query"": ""mutation m{addBook(book:{title:\""New Book\""}){book{title}}}"",""variables"": {}}}");

                // FAILURE: query fails 'execute' step using Websocket
                await SubmitGraphqlWebsocketRequest(url: "/graphql", httpMethod: null, graphQlRequestBody: @"{""type"": ""start"",""id"": ""1"",""payload"": {""query"": ""subscription NotImplementedSub{throwNotImplementedException{name}}"",""variables"": {}}}");

                // FAILURE: query fails 'validate' step using Websocket
                await SubmitGraphqlWebsocketRequest(url: "/graphql", httpMethod: null, graphQlRequestBody: @"{""type"": ""start"",""id"": ""1"",""payload"": {""query"": ""{boook{title author{name}}}"",""variables"": {}}}");
            }

            return expectedGraphQlExecuteSpanCount + expectedAspNetcoreRequestSpanCount;

            void SubmitGraphqlRequest(
                string url,
                string httpMethod,
                string graphQlRequestBody)
            {
                expectedGraphQlExecuteSpanCount++;
                expectedAspNetcoreRequestSpanCount++;

                SubmitRequest(
                    aspNetCorePort,
                    new RequestInfo() { Url = url, HttpMethod = httpMethod, RequestBody = graphQlRequestBody, });
            }

            async Task SubmitGraphqlWebsocketRequest(
                string url,
                string httpMethod,
                string graphQlRequestBody)
            {
                expectedGraphQlExecuteSpanCount++;
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
                await WaitForMessage();
            }
            catch (Exception ex)
            {
                Output.WriteLine($"[websocket] WebSocket connection error: {ex.Message}");
            }
            finally
            {
                try
                {
                    if (webSocket.State == WebSocketState.Open)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                        Output.WriteLine("[websocket] WebSocket connection closed");
                    }
                }
                catch (Exception e)
                {
                    Output.WriteLine("[websocket][debug][err] Failed to close with NormalClosure: " + e.Message);
                }

                webSocket.Dispose();
                Output.WriteLine("[websocket] Websocket connection disposed.");
            }

            async Task WaitForMessage()
            {
                Output.WriteLine("[websocket][debug] Start waiting for a message!");
                Output.WriteLine("[websocket][debug] Websocket state: " + webSocket.State);
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

                    Output.WriteLine("[websocket][debug] Message type: " + res.MessageType);
                    if (res.MessageType == WebSocketMessageType.Close)
                    {
                        Output.WriteLine($"[websocket][debug] Close websocket on message close: {res.CloseStatus}: {res.CloseStatusDescription}");
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                        break;
                    }

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

#endif
