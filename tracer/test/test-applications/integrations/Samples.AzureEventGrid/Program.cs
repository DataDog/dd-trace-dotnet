// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging;
using Azure.Messaging.EventGrid;

namespace Samples.AzureEventGrid
{
    public class Program
    {
        private static readonly string TopicEndpoint =
            Environment.GetEnvironmentVariable("EVENTGRID_TOPIC_ENDPOINT") ??
            "http://localhost:6500/samples-eventgrid-topic/api/events";

        private static readonly string TopicKey =
            Environment.GetEnvironmentVariable("EVENTGRID_TOPIC_KEY") ??
            "test-key";

        public static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Azure Event Grid Test Sample");
            Console.WriteLine($"Connecting to: {TopicEndpoint}");

            var client = new EventGridPublisherClient(
                new Uri(TopicEndpoint),
                new AzureKeyCredential(TopicKey));

            RunWithSpan("EventGridPublisherClient.SendEvent(EventGridEvent)", () => client.SendEvent(CreateEventGridEvent("1")));
            await RunWithSpanAsync("EventGridPublisherClient.SendEventAsync(EventGridEvent)", () => client.SendEventAsync(CreateEventGridEvent("1")));
            RunWithSpan("EventGridPublisherClient.SendEvents(IEnumerable<EventGridEvent>)", () => client.SendEvents(CreateEventGridEvents()));
            await RunWithSpanAsync("EventGridPublisherClient.SendEventsAsync(IEnumerable<EventGridEvent>)", () => client.SendEventsAsync(CreateEventGridEvents()));

            RunWithSpan("EventGridPublisherClient.SendEvent(BinaryData)", () => client.SendEvent(CreateBinaryDataEvent("1")));
            await RunWithSpanAsync("EventGridPublisherClient.SendEventAsync(BinaryData)", () => client.SendEventAsync(CreateBinaryDataEvent("1")));
            RunWithSpan("EventGridPublisherClient.SendEvents(IEnumerable<BinaryData>)", () => client.SendEvents(CreateBinaryDataEvents()));
            await RunWithSpanAsync("EventGridPublisherClient.SendEventsAsync(IEnumerable<BinaryData>)", () => client.SendEventsAsync(CreateBinaryDataEvents()));

            RunWithSpan("EventGridPublisherClient.SendEvent(CloudEvent)", () => client.SendEvent(CreateCloudEvent("1")));
            await RunWithSpanAsync("EventGridPublisherClient.SendEventAsync(CloudEvent)", () => client.SendEventAsync(CreateCloudEvent("1")));
            RunWithSpan("EventGridPublisherClient.SendEvents(IEnumerable<CloudEvent>)", () => client.SendEvents(CreateCloudEvents()));
            await RunWithSpanAsync("EventGridPublisherClient.SendEventsAsync(IEnumerable<CloudEvent>)", () => client.SendEventsAsync(CreateCloudEvents()));

            if (SupportsPartnerChannelOperations())
            {
                // The dynamic calls preserve compilation against pre-4.11 packages, which do not define the partner-channel overloads.
                RunWithSpan("EventGridPublisherClient.SendEvent(CloudEvent, channelName)", () => ((dynamic)client).SendEvent(CreateCloudEvent("1"), "test-channel"));
                RunWithSpan("EventGridPublisherClient.SendEvents(IEnumerable<CloudEvent>, channelName)", () => ((dynamic)client).SendEvents(CreateCloudEvents(), "test-channel"));
                await RunWithSpanAsync("EventGridPublisherClient.SendEventAsync(CloudEvent, channelName)", () => ((dynamic)client).SendEventAsync(CreateCloudEvent("1"), "test-channel"));
                await RunWithSpanAsync("EventGridPublisherClient.SendEventsAsync(IEnumerable<CloudEvent>, channelName)", () => ((dynamic)client).SendEventsAsync(CreateCloudEvents(), "test-channel"));
            }
        }

        private static void RunWithSpan(string spanName, Action action)
        {
            using (SampleHelpers.CreateScope(spanName))
            {
                action();
            }
        }

        private static async Task RunWithSpanAsync(string spanName, Func<Task> action)
        {
            using (SampleHelpers.CreateScope(spanName))
            {
                await action();
            }
        }

        private static bool SupportsPartnerChannelOperations() =>
            typeof(EventGridPublisherClient).GetMethod(
                nameof(EventGridPublisherClient.SendEvents),
                [typeof(IEnumerable<CloudEvent>), typeof(string), typeof(CancellationToken)]) is not null;

        private static IEnumerable<EventGridEvent> CreateEventGridEvents()
        {
            yield return CreateEventGridEvent("1");
            yield return CreateEventGridEvent("2");
            yield return CreateEventGridEvent("3");
        }

        private static IEnumerable<BinaryData> CreateBinaryDataEvents()
        {
            yield return CreateBinaryDataEvent("1");
            yield return CreateBinaryDataEvent("2");
            yield return CreateBinaryDataEvent("3");
        }

        private static IEnumerable<CloudEvent> CreateCloudEvents()
        {
            yield return CreateCloudEvent("1");
            yield return CreateCloudEvent("2");
            yield return CreateCloudEvent("3");
        }

        private static EventGridEvent CreateEventGridEvent(string suffix) =>
            new EventGridEvent(
                subject: $"Samples.AzureEventGrid/test-subject-{suffix}",
                eventType: "Samples.AzureEventGrid.TestEvent",
                dataVersion: "1.0",
                data: new { message = $"Test event {suffix}", timestamp = DateTimeOffset.UtcNow });

        private static BinaryData CreateBinaryDataEvent(string suffix) =>
            new BinaryData(
                JsonSerializer.Serialize(
                    new
                    {
                        id = Guid.NewGuid(),
                        eventType = "Samples.AzureEventGrid.TestEvent",
                        subject = $"Samples.AzureEventGrid/test-subject-{suffix}",
                        eventTime = DateTimeOffset.UtcNow,
                        dataVersion = "1.0",
                        data = new { message = $"Test custom event {suffix}" },
                    }));

        private static CloudEvent CreateCloudEvent(string suffix) =>
            new CloudEvent(
                source: "/Samples.AzureEventGrid/test-source",
                type: "Samples.AzureEventGrid.TestCloudEvent",
                jsonSerializableData: new { message = $"Test cloud event {suffix}", timestamp = DateTimeOffset.UtcNow });
    }
}
