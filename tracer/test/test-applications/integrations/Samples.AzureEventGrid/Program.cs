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

            var client = new EventGridPublisherClient(
                new Uri(TopicEndpoint),
                new AzureKeyCredential(TopicKey));

            switch (testMode)
            {
                case TestMode.SendEventGridEvent:
                    client.SendEvent(CreateEventGridEvent("1"));
                    break;
                case TestMode.SendEventGridEventAsync:
                    await client.SendEventAsync(CreateEventGridEvent("1"));
                    break;
                case TestMode.SendEventGridEvents:
                    client.SendEvents(CreateEventGridEvents());
                    break;
                case TestMode.SendEventGridEventsAsync:
                    await client.SendEventsAsync(CreateEventGridEvents());
                    break;
                case TestMode.SendCloudEvent:
                    client.SendEvent(CreateCloudEvent("1"));
                    break;
                case TestMode.SendCloudEventAsync:
                    await client.SendEventAsync(CreateCloudEvent("1"));
                    break;
                case TestMode.SendCloudEvents:
                    client.SendEvents(CreateCloudEvents());
                    break;
                case TestMode.SendCloudEventsAsync:
                    await client.SendEventsAsync(CreateCloudEvents());
                    break;

                // The dynamic calls preserve compilation against pre-4.11 packages, which do not define the partner-channel overloads.
                case TestMode.SendCloudEventToChannel:
                    ((dynamic)client).SendEvent(CreateCloudEvent("1"), "test-channel");
                    break;
                case TestMode.SendCloudEventsToChannel:
                    ((dynamic)client).SendEvents(CreateCloudEvents(), "test-channel");
                    break;
                case TestMode.SendCloudEventToChannelAsync:
                    await ((dynamic)client).SendEventAsync(CreateCloudEvent("1"), "test-channel");
                    break;
                case TestMode.SendCloudEventsToChannelAsync:
                    await ((dynamic)client).SendEventsAsync(CreateCloudEvents(), "test-channel");
                    break;
                default:
                    throw new ArgumentException($"Unhandled test mode: {testMode}");
            }
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
