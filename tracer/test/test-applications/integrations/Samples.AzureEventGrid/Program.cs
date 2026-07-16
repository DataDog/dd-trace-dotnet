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
        SendCloudEventToChannel,
        SendCloudEventsToChannel,
        SendCloudEventToChannelAsync,
        SendCloudEventsToChannelAsync,
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
                    case TestMode.SendCloudEventToChannel:
                        SendCloudEventToChannel(client);
                        break;
                    case TestMode.SendCloudEventsToChannel:
                        SendCloudEventsToChannel(client);
                        break;
                    case TestMode.SendCloudEventToChannelAsync:
                        await SendCloudEventToChannelAsync(client);
                        break;
                    case TestMode.SendCloudEventsToChannelAsync:
                        await SendCloudEventsToChannelAsync(client);
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
            client.SendEvents(CreateEventGridEvents());
            Console.WriteLine("Sent 3 events successfully");
        }

        private static async Task SendEventGridEventsAsync(EventGridPublisherClient client)
        {
            Console.WriteLine("\n=== SendEventsAsync (EventGridEvent) ===");
            await client.SendEventsAsync(CreateEventGridEvents());
            Console.WriteLine("Sent 3 events successfully");
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
            client.SendEvents(CreateCloudEvents());
            Console.WriteLine("Sent 3 CloudEvents successfully");
        }

        private static async Task SendCloudEventsAsync(EventGridPublisherClient client)
        {
            Console.WriteLine("\n=== SendEventsAsync (CloudEvent) ===");
            await client.SendEventsAsync(CreateCloudEvents());
            Console.WriteLine("Sent 3 CloudEvents successfully");
        }

        private static void SendCloudEventToChannel(EventGridPublisherClient client)
        {
            Console.WriteLine("\n=== SendEvent (CloudEvent with partner channel) ===");
            var ev = CreateCloudEvent("1");
            ((dynamic)client).SendEvent(ev, "test-channel");
            Console.WriteLine("CloudEvent sent successfully");
        }

        private static void SendCloudEventsToChannel(EventGridPublisherClient client)
        {
            Console.WriteLine("\n=== SendEvents (CloudEvent with partner channel) ===");
            ((dynamic)client).SendEvents(CreateCloudEvents(), "test-channel");
            Console.WriteLine("Sent 3 CloudEvents successfully");
        }

        private static async Task SendCloudEventToChannelAsync(EventGridPublisherClient client)
        {
            Console.WriteLine("\n=== SendEventAsync (CloudEvent with partner channel) ===");
            var ev = CreateCloudEvent("1");
            await ((dynamic)client).SendEventAsync(ev, "test-channel");
            Console.WriteLine("CloudEvent sent successfully");
        }

        private static async Task SendCloudEventsToChannelAsync(EventGridPublisherClient client)
        {
            Console.WriteLine("\n=== SendEventsAsync (CloudEvent with partner channel) ===");
            await ((dynamic)client).SendEventsAsync(CreateCloudEvents(), "test-channel");
            Console.WriteLine("Sent 3 CloudEvents successfully");
        }

        private static IEnumerable<EventGridEvent> CreateEventGridEvents()
        {
            yield return CreateEventGridEvent("1");
            yield return CreateEventGridEvent("2");
            yield return CreateEventGridEvent("3");
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

        private static CloudEvent CreateCloudEvent(string suffix) =>
            new CloudEvent(
                source: "/Samples.AzureEventGrid/test-source",
                type: "Samples.AzureEventGrid.TestCloudEvent",
                jsonSerializableData: new { message = $"Test cloud event {suffix}", timestamp = DateTimeOffset.UtcNow });
    }
}
