#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Threading;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;

namespace Samples.AWS.EventBridge
{
    static class SyncHelpers
    {
        private const string EventBusName = "MyEventBus";
        private const string EventDetail = "{\"foo\":\"bar\"}";

        public static void StartEventBridgeTasks(AmazonEventBridgeClient eventBridgeClient)
        {
            Console.WriteLine("Beginning Sync methods");
            using (var scope = SampleHelpers.CreateScope("sync-methods"))
            {
                CreateEventBus(eventBridgeClient, EventBusName);

                // Allow time for the resource to be ready
                Thread.Sleep(1000);

                PutEvents(eventBridgeClient, EventBusName);

                DeleteEventBus(eventBridgeClient, EventBusName);

                // Allow time for the resource to be deleted
                Thread.Sleep(1000);
            }
        }

        private static void PutEvents(AmazonEventBridgeClient eventBridgeClient, string eventBusName)
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

            var response = eventBridgeClient.PutEvents(request);

            Console.WriteLine($"PutEvents(PutEventsRequest) HTTP status code: {response.HttpStatusCode}");
        }

        private static void DeleteEventBus(AmazonEventBridgeClient eventBridgeClient, string eventBusName)
        {
            var deleteEventBusRequest = new DeleteEventBusRequest
            {
                Name = eventBusName
            };

            var response = eventBridgeClient.DeleteEventBus(deleteEventBusRequest);

            Console.WriteLine($"DeleteEventBus(DeleteEventBusRequest) HTTP status code: {response.HttpStatusCode}");
        }

        private static void CreateEventBus(AmazonEventBridgeClient eventBridgeClient, string eventBusName)
        {
            var createEventBusRequest = new CreateEventBusRequest
            {
                Name = eventBusName
            };

            var response = eventBridgeClient.CreateEventBus(createEventBusRequest);

            Console.WriteLine($"CreateEventBus(CreateEventBusRequest) HTTP status code: {response.HttpStatusCode}");
        }
    }
}

#endif
