using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Producer;

namespace Samples.DataStreams.AzureEventHubs
{
    public static class Program
    {
        private static readonly string ConnectionString = Environment.GetEnvironmentVariable("EVENTHUBS_CONNECTION_STRING")
            ?? "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

        private static readonly string EventHubName = Environment.GetEnvironmentVariable("EVENTHUB_NAME") ?? "dsm-test-hub";

        public static async Task Main(string[] args)
        {
            // Creates three pipelines on the same hub, one per publish path:
            // 1. Publish with SendAsync(IEnumerable<EventData>):
            //    (direction:out, topic:dsm-test-hub) -> (direction:in, topic:dsm-test-hub)
            // 2. Publish with CreateBatchAsync/TryAdd/SendAsync(EventDataBatch):
            //    (direction:out, topic:dsm-test-hub) -> (direction:in, topic:dsm-test-hub)
            // 3. Publish with SendAsync(IEnumerable<EventData>, SendEventOptions):
            //    (direction:out, topic:dsm-test-hub) -> (direction:in, topic:dsm-test-hub)
            //
            // In total there are 3 produce checkpoints and 3 consume checkpoints.

            await using var producer = new EventHubProducerClient(ConnectionString, EventHubName);
            await using var consumer = new EventHubConsumerClient(
                EventHubConsumerClient.DefaultConsumerGroupName,
                ConnectionString,
                EventHubName);

            using var eventConsumed = new SemaphoreSlim(0);
            using var stopReading = new CancellationTokenSource();
            var options = new ReadEventOptions { MaximumWaitTime = TimeSpan.FromSeconds(30) };

            // Read only the events this run publishes: the hub keeps the events of earlier runs.
            var readers = new List<Task>();
            foreach (var partitionId in await consumer.GetPartitionIdsAsync())
            {
                var properties = await consumer.GetPartitionPropertiesAsync(partitionId);
                var startingPosition = properties.IsEmpty
                    ? EventPosition.Earliest
                    : EventPosition.FromSequenceNumber(properties.LastEnqueuedSequenceNumber, isInclusive: false);

                readers.Add(Task.Run(async () =>
                {
                    try
                    {
                        await foreach (PartitionEvent partitionEvent in consumer.ReadEventsFromPartitionAsync(partitionId, startingPosition, options, stopReading.Token))
                        {
                            if (partitionEvent.Data == null)
                            {
                                continue;
                            }

                            var body = Encoding.UTF8.GetString(partitionEvent.Data.EventBody.ToArray());
                            Console.WriteLine($"Received: '{body}'");

                            eventConsumed.Release();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }));
            }

            // Pipeline 1: publish with SendAsync(IEnumerable<EventData>)
            await producer.SendAsync(new[] { new EventData(Encoding.UTF8.GetBytes("event 1")) });

            // Wait for each event to be consumed so the publishes are never read in one batch
            await eventConsumed.WaitAsync();

            // Pipeline 2: publish with CreateBatchAsync/TryAdd/SendAsync(EventDataBatch)
            using var batch = await producer.CreateBatchAsync();
            batch.TryAdd(new EventData(Encoding.UTF8.GetBytes("event 2")));
            await producer.SendAsync(batch);
            await eventConsumed.WaitAsync();

            // Pipeline 3: publish with SendAsync(IEnumerable<EventData>, SendEventOptions)
            await producer.SendAsync(new[] { new EventData(Encoding.UTF8.GetBytes("event 3")) }, new SendEventOptions());
            await eventConsumed.WaitAsync();

            stopReading.Cancel();
            await Task.WhenAll(readers);
            Console.WriteLine("Done");
        }
    }
}
