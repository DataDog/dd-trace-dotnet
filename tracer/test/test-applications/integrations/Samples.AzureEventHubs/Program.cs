using System;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Producer;

namespace Samples.AzureEventHubs
{
    public enum TestMode
    {
        TestEventHubsMessageBatch,
        TestEventHubsEnumerable,
        TestEventHubsBufferedProducer
    }

    public class Program
    {
        private static readonly string ConnectionString =
            Environment.GetEnvironmentVariable("EVENTHUBS_CONNECTION_STRING") ??
            "Endpoint=sb://localhost:5673;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
        private const string EventHubName = "samples-eventhubs-hub";
        private const string ConsumerGroup = "cg1";

        public static async Task Main(string[] args)
        {
            var testModeString = Environment.GetEnvironmentVariable("EVENTHUBS_TEST_MODE");
            Console.WriteLine($"Starting Azure EventHubs Test Sample - Mode: {testModeString ?? "Default"}");
            Console.WriteLine($"Connecting to: {ConnectionString}");
            Console.WriteLine($"Using EventHub: {EventHubName}");

            try
            {
                var producerClient = new EventHubProducerClient(ConnectionString, EventHubName);
                var consumerClient = new EventHubConsumerClient(ConsumerGroup, ConnectionString, EventHubName);
                Console.WriteLine("EventHubs producer and consumer clients created successfully");

                var properties = await producerClient.GetEventHubPropertiesAsync();
                Console.WriteLine($"EventHub properties retrieved: Name={properties.Name}, PartitionCount={properties.PartitionIds.Length}");

                if (string.IsNullOrEmpty(testModeString) || !Enum.TryParse<TestMode>(testModeString, ignoreCase: true, out var testMode))
                {
                    throw new ArgumentException($"Invalid or missing EVENTHUBS_TEST_MODE. Expected one of: {string.Join(", ", Enum.GetNames(typeof(TestMode)))}. Got: '{testModeString ?? "null"}'");
                }

                switch (testMode)
                {
                    case TestMode.TestEventHubsMessageBatch:
                        await TestEventHubsMessageBatchAsync(producerClient, consumerClient);
                        break;
                    case TestMode.TestEventHubsEnumerable:
                        await TestEventHubsEnumerableAsync(producerClient, consumerClient);
                        break;
                    case TestMode.TestEventHubsBufferedProducer:
                        await TestEventHubsBufferedProducerAsync(consumerClient);
                        break;
                    default:
                        throw new ArgumentException($"Unhandled test mode: {testMode}");
                }

                await producerClient.DisposeAsync();
                await consumerClient.DisposeAsync();
                Console.WriteLine("Resources disposed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }

            Console.WriteLine("Azure EventHubs Test Sample completed successfully");
        }

        private static async Task TestEventHubsMessageBatchAsync(EventHubProducerClient producerClient, EventHubConsumerClient consumerClient)
        {
            Console.WriteLine("\n=== EventHubs Message Batch Test ===");

            var partitionIds = await consumerClient.GetPartitionIdsAsync();
            Console.WriteLine("Creating event batch...");
            using var eventBatch = await producerClient.CreateBatchAsync();

            var events = new[]
            {
                new EventData(Encoding.UTF8.GetBytes("Batch event 1"))
                {
                    MessageId = Guid.NewGuid().ToString(),
                    ContentType = "BatchTest1"
                },
                new EventData(Encoding.UTF8.GetBytes("Batch event 2"))
                {
                    MessageId = Guid.NewGuid().ToString(),
                    ContentType = "BatchTest2"
                },
                new EventData(Encoding.UTF8.GetBytes("Batch event 3"))
                {
                    MessageId = Guid.NewGuid().ToString(),
                    ContentType = "BatchTest3"
                }
            };

            Console.WriteLine("Adding events to batch using TryAdd...");
            for (int i = 0; i < events.Length; i++)
            {
                var added = eventBatch.TryAdd(events[i]);
                Console.WriteLine($"Event {i + 1} (ID: {events[i].MessageId}) added to batch: {added}");
            }

            Console.WriteLine($"Batch now contains {eventBatch.Count} events");

            var sendTime = DateTimeOffset.UtcNow;

            Console.WriteLine("Sending event batch...");
            await producerClient.SendAsync(eventBatch);
            Console.WriteLine($"Successfully sent batch with {eventBatch.Count} events");

            Console.WriteLine("Receiving events from batch...");
            Console.WriteLine($"Found {partitionIds.Length} partitions to read from");

            int totalEventsReceived = 0;
            var readTimeout = TimeSpan.FromSeconds(10);

            foreach (var partitionId in partitionIds)
            {
                Console.WriteLine($"Reading from partition {partitionId}...");
                var startTime = DateTime.UtcNow;

                try
                {
                    await foreach (PartitionEvent partitionEvent in consumerClient.ReadEventsFromPartitionAsync(
                        partitionId,
                        EventPosition.FromEnqueuedTime(sendTime),
                        new ReadEventOptions { MaximumWaitTime = readTimeout }))
                    {
                        if (partitionEvent.Data != null)
                        {
                            Console.WriteLine($"Processing event ID: {partitionEvent.Data.MessageId}");
                            totalEventsReceived++;

                            if (totalEventsReceived >= eventBatch.Count)
                            {
                                break;
                            }
                        }

                        if (DateTime.UtcNow - startTime > readTimeout)
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading from partition {partitionId}: {ex.Message}");
                }

                if (totalEventsReceived >= eventBatch.Count)
                {
                    break;
                }
            }

            Console.WriteLine($"Completed processing {totalEventsReceived} events");
            Console.WriteLine("EventHubs Message Batch test completed");
        }

        private static async Task TestEventHubsEnumerableAsync(EventHubProducerClient producerClient, EventHubConsumerClient consumerClient)
        {
            Console.WriteLine("\n=== EventHubs Enumerable Test ===");

            var partitionIds = await consumerClient.GetPartitionIdsAsync();

            var messageCountString = Environment.GetEnvironmentVariable("EVENTHUBS_MESSAGE_COUNT");
            var messageCount = int.TryParse(messageCountString, out var parsedCount) ? parsedCount : 1;

            Console.WriteLine($"Creating enumerable of {messageCount} event(s)...");

            var events = new EventData[messageCount];
            for (int i = 0; i < messageCount; i++)
            {
                events[i] = new EventData(Encoding.UTF8.GetBytes($"Enumerable event {i + 1}"))
                {
                    MessageId = Guid.NewGuid().ToString(),
                    ContentType = $"EnumerableTest{i + 1}"
                };
            }

            var sendTime = DateTimeOffset.UtcNow;

            Console.WriteLine("Sending enumerable of events...");
            await producerClient.SendAsync(events);
            Console.WriteLine($"Successfully sent enumerable with {events.Length} events");

            Console.WriteLine("Receiving events from enumerable...");
            Console.WriteLine($"Found {partitionIds.Length} partitions to read from");

            int totalEventsReceived = 0;
            var readTimeout = TimeSpan.FromSeconds(10);

            foreach (var partitionId in partitionIds)
            {
                Console.WriteLine($"Reading from partition {partitionId}...");
                var startTime = DateTime.UtcNow;

                try
                {
                    await foreach (PartitionEvent partitionEvent in consumerClient.ReadEventsFromPartitionAsync(
                        partitionId,
                        EventPosition.FromEnqueuedTime(sendTime),
                        new ReadEventOptions { MaximumWaitTime = readTimeout }))
                    {
                        if (partitionEvent.Data != null)
                        {
                            Console.WriteLine($"Processing event ID: {partitionEvent.Data.MessageId}");
                            totalEventsReceived++;

                            if (totalEventsReceived >= events.Length)
                            {
                                break;
                            }
                        }

                        if (DateTime.UtcNow - startTime > readTimeout)
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading from partition {partitionId}: {ex.Message}");
                }

                if (totalEventsReceived >= events.Length)
                {
                    break;
                }
            }

            Console.WriteLine($"Completed processing {totalEventsReceived} events");
            Console.WriteLine("EventHubs Enumerable test completed");
        }

        private static async Task TestEventHubsBufferedProducerAsync(EventHubConsumerClient consumerClient)
        {
            Console.WriteLine("\n=== EventHubs Buffered Producer Test ===");

            var partitionIds = await consumerClient.GetPartitionIdsAsync();
            Console.WriteLine("Creating buffered producer client...");

            var bufferedProducerClient = new EventHubBufferedProducerClient(ConnectionString, EventHubName);

            var successCount = 0;
            var failureCount = 0;

            bufferedProducerClient.SendEventBatchSucceededAsync += args =>
            {
                Console.WriteLine($"Batch succeeded: {args.EventBatch.Count} events sent to partition {args.PartitionId}");
                successCount += args.EventBatch.Count;
                return Task.CompletedTask;
            };

            bufferedProducerClient.SendEventBatchFailedAsync += args =>
            {
                Console.WriteLine($"Batch failed: {args.EventBatch.Count} events for partition {args.PartitionId}, Error: {args.Exception.Message}");
                failureCount += args.EventBatch.Count;
                return Task.CompletedTask;
            };

            Console.WriteLine("Enqueueing events to buffered producer...");

            var events = new[]
            {
                new EventData(Encoding.UTF8.GetBytes("Buffered event 1"))
                {
                    MessageId = Guid.NewGuid().ToString(),
                    ContentType = "BufferedTest1"
                },
                new EventData(Encoding.UTF8.GetBytes("Buffered event 2"))
                {
                    MessageId = Guid.NewGuid().ToString(),
                    ContentType = "BufferedTest2"
                },
                new EventData(Encoding.UTF8.GetBytes("Buffered event 3"))
                {
                    MessageId = Guid.NewGuid().ToString(),
                    ContentType = "BufferedTest3"
                }
            };

            var sendTime = DateTimeOffset.UtcNow;

            Console.WriteLine("Enqueueing events...");
            await bufferedProducerClient.EnqueueEventsAsync(events);
            Console.WriteLine($"Successfully enqueued {events.Length} events");

            Console.WriteLine("Flushing buffered producer...");
            await bufferedProducerClient.FlushAsync();
            Console.WriteLine("Flush completed");

            Console.WriteLine($"Success count: {successCount}, Failure count: {failureCount}");

            Console.WriteLine("Receiving events from buffered producer...");
            Console.WriteLine($"Found {partitionIds.Length} partitions to read from");

            int totalEventsReceived = 0;
            var readTimeout = TimeSpan.FromSeconds(10);

            foreach (var partitionId in partitionIds)
            {
                Console.WriteLine($"Reading from partition {partitionId}...");
                var startTime = DateTime.UtcNow;

                try
                {
                    await foreach (PartitionEvent partitionEvent in consumerClient.ReadEventsFromPartitionAsync(
                        partitionId,
                        EventPosition.FromEnqueuedTime(sendTime),
                        new ReadEventOptions { MaximumWaitTime = readTimeout }))
                    {
                        if (partitionEvent.Data != null)
                        {
                            Console.WriteLine($"Processing event ID: {partitionEvent.Data.MessageId}");
                            totalEventsReceived++;

                            if (totalEventsReceived >= events.Length)
                            {
                                break;
                            }
                        }

                        if (DateTime.UtcNow - startTime > readTimeout)
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading from partition {partitionId}: {ex.Message}");
                }

                if (totalEventsReceived >= events.Length)
                {
                    break;
                }
            }

            Console.WriteLine($"Completed processing {totalEventsReceived} events");

            await bufferedProducerClient.DisposeAsync();
            Console.WriteLine("Buffered producer disposed");
            Console.WriteLine("EventHubs Buffered Producer test completed");
        }
    }
}
