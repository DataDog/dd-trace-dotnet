using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;

namespace Samples.AWS.EventBridge
{
    static class AsyncHelpers
    {
        private const string EventBusName = "MyEventBus";
        private const string EventDetail = "{\"foo\":\"bar\"}";

        public static async Task StartEventBridgeTasks(AmazonEventBridgeClient eventBridgeClient)
        {
            Console.WriteLine("Beginning Async methods");
            using (var scope = SampleHelpers.CreateScope("async-methods"))
            {
                await CreateEventBusAsync(eventBridgeClient, EventBusName);

                // Allow time for the resource to be ready
                Thread.Sleep(1000);

                await PutEventsAsync(eventBridgeClient, EventBusName);

                await DeleteEventBusAsync(eventBridgeClient, EventBusName);

                // Allow time for the resource to be deleted
                Thread.Sleep(1000);
            }
        }

        private static async Task PutEventsAsync(AmazonEventBridgeClient eventBridgeClient, string eventBusName)
        {
            var request = new PutEventsRequest
            {
                Entries = new List<PutEventsRequestEntry>
                {
                    new PutEventsRequestEntry
                    {
                        Source = "my.app",
                        DetailType = "appEvent",
                        Detail = EventDetail,
                        EventBusName = eventBusName
                    }
                }
            };

            var response = await eventBridgeClient.PutEventsAsync(request);

            Console.WriteLine($"PutEventsAsync(PutEventsRequest) HTTP status code: {response.HttpStatusCode}");
        }

        private static async Task DeleteEventBusAsync(AmazonEventBridgeClient eventBridgeClient, string eventBusName)
        {
            var deleteEventBusRequest = new DeleteEventBusRequest
            {
                Name = eventBusName
            };

            var response = await eventBridgeClient.DeleteEventBusAsync(deleteEventBusRequest);

            Console.WriteLine($"DeleteEventBusAsync(DeleteEventBusRequest) HTTP status code: {response.HttpStatusCode}");
        }

        private static async Task CreateEventBusAsync(AmazonEventBridgeClient eventBridgeClient, string eventBusName)
        {
            var createEventBusRequest = new CreateEventBusRequest
            {
                Name = eventBusName
            };

            var response = await eventBridgeClient.CreateEventBusAsync(createEventBusRequest);

            Console.WriteLine($"CreateEventBusAsync(CreateEventBusRequest) HTTP status code: {response.HttpStatusCode}");
        }
    }
}
