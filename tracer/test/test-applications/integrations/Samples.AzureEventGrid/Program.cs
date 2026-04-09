// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging;
using Azure.Messaging.EventGrid;

namespace Samples.AzureEventGrid
{
    public enum TestMode
    {
        SendEventGridEvent,
        SendEventGridEventAsync,
        SendEventGridEvents,
        SendEventGridEventsAsync,
        SendCloudEvent,
        SendCloudEventAsync,
        SendCloudEvents,
        SendCloudEventsAsync,
    }

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
            var testModeString = Environment.GetEnvironmentVariable("EVENTGRID_TEST_MODE");
            Console.WriteLine($"Starting Azure Event Grid Test Sample - Mode: {testModeString ?? "Default"}");
            Console.WriteLine($"Connecting to: {TopicEndpoint}");

            if (string.IsNullOrEmpty(testModeString) || !Enum.TryParse<TestMode>(testModeString, ignoreCase: true, out var testMode))
            {
                throw new ArgumentException($"Invalid or missing EVENTGRID_TEST_MODE. Expected one of: {string.Join(", ", Enum.GetNames(typeof(TestMode)))}. Got: '{testModeString ?? "null"}'");
            }

            try
            {
                var client = new EventGridPublisherClient(
                    new Uri(TopicEndpoint),
                    new AzureKeyCredential(TopicKey));

                Console.WriteLine("EventGridPublisherClient created successfully");

                switch (testMode)
                {
                    case TestMode.SendEventGridEvent:
                        SendEventGridEvent(client);
                        break;
                    case TestMode.SendEventGridEventAsync:
                        await SendEventGridEventAsync(client);
                        break;
                    case TestMode.SendEventGridEvents:
                        SendEventGridEvents(client);
                        break;
                    case TestMode.SendEventGridEventsAsync:
                        await SendEventGridEventsAsync(client);
                        break;
                    case TestMode.SendCloudEvent:
                        SendCloudEvent(client);
                        break;
                    case TestMode.SendCloudEventAsync:
                        await SendCloudEventAsync(client);
                        break;
                    case TestMode.SendCloudEvents:
                        SendCloudEvents(client);
                        break;
                    case TestMode.SendCloudEventsAsync:
                        await SendCloudEventsAsync(client);
                        break;
                    default:
                        throw new ArgumentException($"Unhandled test mode: {testMode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }

            Console.WriteLine("Azure Event Grid Test Sample completed successfully");
        }

        private static void SendEventGridEvent(EventGridPublisherClient client)
        {
            Console.WriteLine("\n=== SendEvent (EventGridEvent) ===");
            var ev = CreateEventGridEvent("1");
            client.SendEvent(ev);
            Console.WriteLine("Event sent successfully");
        }

        private static async Task SendEventGridEventAsync(EventGridPublisherClient client)
        {
            Console.WriteLine("\n=== SendEventAsync (EventGridEvent) ===");
            var ev = CreateEventGridEvent("1");
            await client.SendEventAsync(ev);
            Console.WriteLine("Event sent successfully");
        }

        private static void SendEventGridEvents(EventGridPublisherClient client)
        {
            Console.WriteLine("\n=== SendEvents (EventGridEvent) ===");
            var events = new List<EventGridEvent>
            {
                CreateEventGridEvent("1"),
                CreateEventGridEvent("2"),
                CreateEventGridEvent("3"),
            };
            client.SendEvents(events);
            Console.WriteLine($"Sent {events.Count} events successfully");
        }

        private static async Task SendEventGridEventsAsync(EventGridPublisherClient client)
        {
            Console.WriteLine("\n=== SendEventsAsync (EventGridEvent) ===");
            var events = new List<EventGridEvent>
            {
                CreateEventGridEvent("1"),
                CreateEventGridEvent("2"),
                CreateEventGridEvent("3"),
            };
            await client.SendEventsAsync(events);
            Console.WriteLine($"Sent {events.Count} events successfully");
        }

        private static void SendCloudEvent(EventGridPublisherClient client)
        {
            Console.WriteLine("\n=== SendEvent (CloudEvent) ===");
            var ev = CreateCloudEvent("1");
            client.SendEvent(ev);
            Console.WriteLine("CloudEvent sent successfully");
        }

        private static async Task SendCloudEventAsync(EventGridPublisherClient client)
        {
            Console.WriteLine("\n=== SendEventAsync (CloudEvent) ===");
            var ev = CreateCloudEvent("1");
            await client.SendEventAsync(ev);
            Console.WriteLine("CloudEvent sent successfully");
        }

        private static void SendCloudEvents(EventGridPublisherClient client)
        {
            Console.WriteLine("\n=== SendEvents (CloudEvent) ===");
            var events = new List<CloudEvent>
            {
                CreateCloudEvent("1"),
                CreateCloudEvent("2"),
                CreateCloudEvent("3"),
            };
            client.SendEvents(events);
            Console.WriteLine($"Sent {events.Count} CloudEvents successfully");
        }

        private static async Task SendCloudEventsAsync(EventGridPublisherClient client)
        {
            Console.WriteLine("\n=== SendEventsAsync (CloudEvent) ===");
            var events = new List<CloudEvent>
            {
                CreateCloudEvent("1"),
                CreateCloudEvent("2"),
                CreateCloudEvent("3"),
            };
            await client.SendEventsAsync(events);
            Console.WriteLine($"Sent {events.Count} CloudEvents successfully");
        }

        private static EventGridEvent CreateEventGridEvent(string suffix) =>
            new EventGridEvent(
                subject: $"Samples.AzureEventGrid/test-subject-{suffix}",
                eventType: "Samples.AzureEventGrid.TestEvent",
                dataVersion: "1.0",
                data: new { message = $"Test event {suffix}", timestamp = DateTimeOffset.UtcNow });

        private static CloudEvent CreateCloudEvent(string suffix) =>
            new CloudEvent(
                source: "/Samples.AzureEventGrid/test-source",
                type: "Samples.AzureEventGrid.TestCloudEvent",
                jsonSerializableData: new { message = $"Test cloud event {suffix}", timestamp = DateTimeOffset.UtcNow });
    }
}
