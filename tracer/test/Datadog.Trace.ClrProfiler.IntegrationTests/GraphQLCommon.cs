// <copyright file="GraphQLCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public static class GraphQLCommon
    {
        public static void SubmitRequest(ITestOutputHelper output, int aspNetCorePort, RequestInfo requestInfo, bool printResponseText = true)
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
                        output.WriteLine(ex.ToString());
                    }

                    if (printResponseText)
                    {
                        output.WriteLine($"[http] {response.StatusCode} {responseText}");
                    }
                }
            }
            catch (WebException wex)
            {
                output.WriteLine($"[http] exception: {wex}");
                if (wex.Response is HttpWebResponse response)
                {
                    using (var stream = response.GetResponseStream())
                    using (var reader = new StreamReader(stream))
                    {
                        output.WriteLine($"[http] {response.StatusCode} {reader.ReadToEnd()}");
                    }
                }
            }
        }

        public static async Task SubmitWebsocketRequest(ITestOutputHelper output, int aspNetCorePort, RequestInfo requestInfo)
        {
            var uri = new Uri($"ws://localhost:{aspNetCorePort}{requestInfo.Url}");
            var webSocket = new ClientWebSocket();
            webSocket.Options.AddSubProtocol("graphql-ws");

            try
            {
                await webSocket.ConnectAsync(uri, CancellationToken.None);
                output.WriteLine("[websocket] WebSocket connection established");

                // GraphQL First packet initialization
                const string initPayload = @"{
                    ""type"": ""connection_init"",
                    ""payload"": {""Accept"":""application/json""}
                }";
                var initBuffer = System.Text.Encoding.UTF8.GetBytes(initPayload);
                var initSegment = new ArraySegment<byte>(initBuffer);
                await webSocket.SendAsync(initSegment, WebSocketMessageType.Text, true, CancellationToken.None);
                output.WriteLine("[websocket] Connection initialized (init packet sent) 1/2");
                await WaitForMessage();
                output.WriteLine("[websocket] Connection initialized (init packet received) 2/2");

                // Send test request
                output.WriteLine($"[websocket] Send request: {requestInfo.RequestBody}");
                var buffer = System.Text.Encoding.UTF8.GetBytes(requestInfo.RequestBody);
                var segment = new ArraySegment<byte>(buffer);
                await webSocket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                output.WriteLine("[websocket] Request sent");
                output.WriteLine("[websocket] Waiting for the response:");
                await WaitForMessage();
                output.WriteLine("[websocket] Response received.");

                // Terminate the GraphQl Websocket connection
                output.WriteLine("[websocket] Send connection_terminate...");
                const string terminatePayload = @"{
                    ""type"": ""connection_terminate"",
                    ""payload"": {}
                }";
                var terminateBuffer = System.Text.Encoding.UTF8.GetBytes(terminatePayload);
                var terminateSegment = new ArraySegment<byte>(terminateBuffer);
                await webSocket.SendAsync(terminateSegment, WebSocketMessageType.Text, true, CancellationToken.None);
                output.WriteLine("[websocket] connection_terminate sent");
                await WaitForMessage(true);
            }
            catch (Exception ex)
            {
                output.WriteLine($"[websocket] WebSocket connection error: {ex.Message}");
            }
            finally
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    output.WriteLine("[websocket] WebSocket connection closed");
                }

                webSocket.Dispose();
                output.WriteLine("[websocket] Websocket connection disposed.");
            }

            async Task WaitForMessage(bool endOfConnection = false)
            {
                output.WriteLine("[websocket][debug] Start waiting for a message!");
                output.WriteLine("[websocket][debug] Websocket state: " + webSocket.State);
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

                    output.WriteLine("[websocket][debug] Message type: " + res.MessageType);
                    if (res.MessageType == WebSocketMessageType.Close)
                    {
                        output.WriteLine($"[websocket][debug] Close websocket on message close: {res.CloseStatus}: {res.CloseStatusDescription}");
                        await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                        break;
                    }

                    // Message received from the websocket
                    var msgString = Encoding.UTF8.GetString(ms.ToArray());
                    ms = new MemoryStream(); // Reset Stream

                    if (msgString.Length == 0)
                    {
                        output.WriteLine("[websocket][debug] Received an empty message.");
                        continue;
                    }

                    // Check if the data received is a 'complete' message
                    output.WriteLine("[websocket] before json: " + msgString);
                    var jsonObj = JObject.Parse(msgString);
                    var typeData = jsonObj.GetValue("type")?.Value<string>();
                    if (typeData is "complete" or "error" or "connection_ack")
                    {
                        output.WriteLine("[websocket][debug] Complete or Error or Connection_ack Message");

                        if (endOfConnection)
                        {
                            continue;
                        }

                        break;
                    }
                }

                output.WriteLine("[websocket] Stopped waiting for a message");
            }
        }

        public class RequestInfo
        {
            public string Url { get; set; }

            public string HttpMethod { get; set; }

            public string RequestBody { get; set; }
        }
    }
}
