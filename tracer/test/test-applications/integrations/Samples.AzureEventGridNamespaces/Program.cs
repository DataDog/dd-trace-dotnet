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
using Datadog.Trace;

namespace Samples.AzureEventGridNamespaces
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            using (var server = WebServer.Start(out var endpoint))
            {
                server.RequestHandler = ValidateCloudEventRequest;

                var client = new EventGridSenderClient(
                    new Uri(endpoint),
                    "samples-eventgrid-topic",
                    new AzureKeyCredential("test-key"));

                RunWithSpan("EventGridSenderClient.Send(CloudEvent)", () => client.Send(CreateCloudEvent("1")));
                await RunWithSpanAsync("EventGridSenderClient.SendAsync(CloudEvent)", () => client.SendAsync(CreateCloudEvent("1")));
                RunWithSpan("EventGridSenderClient.Send(IEnumerable<CloudEvent>)", () => client.Send(CreateCloudEvents()));
                await RunWithSpanAsync("EventGridSenderClient.SendAsync(IEnumerable<CloudEvent>)", () => client.SendAsync(CreateCloudEvents()));
            }

            await SampleHelpers.ForceTracerFlushAsync();
        }

        private static void RunWithSpan(string spanName, Action action)
        {
            using (Tracer.Instance.StartActive(spanName))
            {
                action();
            }
        }

        private static async Task RunWithSpanAsync(string spanName, Func<Task> action)
        {
            using (Tracer.Instance.StartActive(spanName))
            {
                await action();
            }
        }

        private static IEnumerable<CloudEvent> CreateCloudEvents()
        {
            yield return CreateCloudEvent("1");
            yield return CreateCloudEvent("2");
            yield return CreateCloudEvent("3");
        }

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
