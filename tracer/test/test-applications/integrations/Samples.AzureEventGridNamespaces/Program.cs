// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging;
using Azure.Messaging.EventGrid.Namespaces;

namespace Samples.AzureEventGridNamespaces
{
    public enum TestMode
    {
        Send,
        SendAsync,
        SendBatch,
        SendBatchAsync,
    }

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var testModeString = Environment.GetEnvironmentVariable("EVENTGRID_TEST_MODE");
            if (string.IsNullOrEmpty(testModeString) || !Enum.TryParse<TestMode>(testModeString, ignoreCase: true, out var testMode))
            {
                throw new ArgumentException($"Invalid or missing EVENTGRID_TEST_MODE. Expected one of: {string.Join(", ", Enum.GetNames(typeof(TestMode)))}. Got: '{testModeString ?? "null"}'");
            }

            using (var server = WebServer.Start(out var endpoint))
            {
                server.RequestHandler = ValidateCloudEventRequest;

                var client = new EventGridSenderClient(
                    new Uri(endpoint),
                    "samples-eventgrid-topic",
                    new AzureKeyCredential("test-key"));

                switch (testMode)
                {
                    case TestMode.Send:
                        client.Send(CreateCloudEvent("1"));
                        break;
                    case TestMode.SendAsync:
                        await client.SendAsync(CreateCloudEvent("1"));
                        break;
                    case TestMode.SendBatch:
                        client.Send(CreateCloudEvents());
                        break;
                    case TestMode.SendBatchAsync:
                        await client.SendAsync(CreateCloudEvents());
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(testMode), testMode, "Unhandled test mode");
                }
            }

            await SampleHelpers.ForceTracerFlushAsync();
        }

        private static List<CloudEvent> CreateCloudEvents() =>
        [
            CreateCloudEvent("1"),
            CreateCloudEvent("2"),
            CreateCloudEvent("3"),
        ];

        private static CloudEvent CreateCloudEvent(string suffix) =>
            new CloudEvent(
                source: "/Samples.AzureEventGridNamespaces/test-source",
                type: "Samples.AzureEventGridNamespaces.TestCloudEvent",
                jsonSerializableData: new { message = $"Test cloud event {suffix}", timestamp = DateTimeOffset.UtcNow });

        private static void ValidateCloudEventRequest(HttpListenerContext context)
        {
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                var requestContent = reader.ReadToEnd();
                Console.WriteLine($"Event Grid namespace request content: {requestContent}");

                if (requestContent.IndexOf("\"traceparent\"", StringComparison.Ordinal) < 0)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    context.Response.Close();
                    return;
                }
            }

            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.Close();
        }
    }
}
