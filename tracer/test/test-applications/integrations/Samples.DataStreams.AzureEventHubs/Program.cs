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
            // Creates two pipelines on the same hub, one per publish path:
            // 1. Publish with SendAsync(IEnumerable<EventData>):
            //    (direction:out, topic:dsm-test-hub) -> (direction:in, topic:dsm-test-hub)
            // 2. Publish with CreateBatchAsync/TryAdd/SendAsync(EventDataBatch):
            //    (direction:out, topic:dsm-test-hub) -> (direction:in, topic:dsm-test-hub)
            //
            // In total there are 2 produce checkpoints and 2 consume checkpoints.

            await using var producer = new EventHubProducerClient(ConnectionString, EventHubName);
            await using var consumer = new EventHubConsumerClient(
                EventHubConsumerClient.DefaultConsumerGroupName,
                ConnectionString,
                EventHubName);

            var firstEventConsumed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondEventConsumed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var consumedCount = 0;

            var consumeTask = Task.Run(async () =>
            {
                var options = new ReadEventOptions { MaximumWaitTime = TimeSpan.FromSeconds(30) };

                await foreach (PartitionEvent partitionEvent in consumer.ReadEventsAsync(options))
                {
                    if (partitionEvent.Data == null)
                    {
                        continue;
                    }

                    var body = Encoding.UTF8.GetString(partitionEvent.Data.EventBody.ToArray());
                    Console.WriteLine($"Received: '{body}'");

                    var index = Interlocked.Increment(ref consumedCount) - 1;
                    if (index == 0)
                    {
                        firstEventConsumed.TrySetResult(true);
                    }
                    else
                    {
                        secondEventConsumed.TrySetResult(true);
                        break;
                    }
                }
            });

            // Pipeline 1: publish with SendAsync(IEnumerable<EventData>)
            await producer.SendAsync(new[] { new EventData(Encoding.UTF8.GetBytes("event 1")) });

            // Wait for the first event to be consumed so the two publishes are never read in one batch
            await firstEventConsumed.Task;

            // Pipeline 2: publish with CreateBatchAsync/TryAdd/SendAsync(EventDataBatch)
            using var batch = await producer.CreateBatchAsync();
            batch.TryAdd(new EventData(Encoding.UTF8.GetBytes("event 2")));
            await producer.SendAsync(batch);

            await secondEventConsumed.Task;
            Console.WriteLine("Done");
        }
    }
}
