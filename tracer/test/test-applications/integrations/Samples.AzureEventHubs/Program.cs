using System;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Producer;

namespace Samples.AzureEventHubs
{
    public class Program
    {
        private static readonly string ConnectionString =
            Environment.GetEnvironmentVariable("EVENTHUBS_CONNECTION_STRING") ??
            "Endpoint=sb://localhost:5673;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
        private const string EventHubName = "samples-eventhubs-hub";
        private const string ConsumerGroup = "cg1";

        public static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Azure EventHubs Test Sample");
            Console.WriteLine($"Connecting to: {ConnectionString}");
            Console.WriteLine($"Using EventHub: {EventHubName}");

            try
            {
                var producerClient = new EventHubProducerClient(ConnectionString, EventHubName);
                var consumerClient = new EventHubConsumerClient(ConsumerGroup, ConnectionString, EventHubName);
                Console.WriteLine("EventHubs producer and consumer clients created successfully");

                // Test basic connection
                var properties = await producerClient.GetEventHubPropertiesAsync();
                Console.WriteLine($"EventHub properties retrieved: Name={properties.Name}, PartitionCount={properties.PartitionIds.Length}");

                Console.WriteLine("\n=== Single Event Operations ===");

                // Single event
                var singleEvent = new EventData(Encoding.UTF8.GetBytes("Hello from EventHubs test!"))
                {
                    MessageId = Guid.NewGuid().ToString()
                };
                singleEvent.Properties["TestType"] = "Single";

                Console.WriteLine($"Sending single event with ID: {singleEvent.MessageId}");
                await producerClient.SendAsync(new[] { singleEvent });
                Console.WriteLine("Single event sent successfully");

                Console.WriteLine("\n=== EventDataBatch Operations ===");

                // Create an EventDataBatch
                var batch = await producerClient.CreateBatchAsync();
                Console.WriteLine("EventDataBatch created successfully");

                // Add events to batch (this should trigger EventDataBatchTryAddIntegration)
                for (int i = 0; i < 3; i++)
                {
                    var eventData = new EventData(Encoding.UTF8.GetBytes($"Batch event {i}"));
                    eventData.MessageId = Guid.NewGuid().ToString();
                    eventData.Properties["EventNumber"] = i;
                    eventData.Properties["TestType"] = "Batch";

                    bool added = batch.TryAdd(eventData);
                    Console.WriteLine($"Added event {i} to batch: {added}");
                }

                Console.WriteLine($"Batch ready with {batch.Count} events, size: {batch.SizeInBytes} bytes");

                // Send the batch (this should trigger EventHubProducerClientSendBatchAsyncIntegration)
                Console.WriteLine("Sending EventDataBatch...");
                await producerClient.SendAsync(batch);
                Console.WriteLine("EventDataBatch sent successfully");

                Console.WriteLine("\n=== Consumer Operations ===");

                // Read events from all partitions (starting from earliest to catch our sent events)
                Console.WriteLine("Attempting to read events from all partitions...");
                var partitionIds = await consumerClient.GetPartitionIdsAsync();
                Console.WriteLine($"Found {partitionIds.Length} partitions");

                int totalEventsReceived = 0;
                var readTimeout = TimeSpan.FromSeconds(3);

                foreach (var partitionId in partitionIds)
                {
                    Console.WriteLine($"Reading from partition {partitionId}...");

                    try
                    {
                        // Read events from this partition (should trigger consumer integration)
                        var startTime = DateTime.UtcNow;
                        await foreach (PartitionEvent partitionEvent in consumerClient.ReadEventsFromPartitionAsync(
                            partitionId,
                            EventPosition.Earliest,
                            new ReadEventOptions { MaximumWaitTime = readTimeout }))
                        {
                            if (partitionEvent.Data != null)
                            {
                                var body = partitionEvent.Data.EventBody.ToString();
                                var messageId = partitionEvent.Data.MessageId;
                                var testType = partitionEvent.Data.Properties.ContainsKey("TestType")
                                    ? partitionEvent.Data.Properties["TestType"]
                                    : "Unknown";

                                Console.WriteLine($"Received event from partition {partitionId}: ID={messageId}, Type={testType}, Body={body}");
                                totalEventsReceived++;
                            }

                            // Break after reasonable timeout or limit
                            if (DateTime.UtcNow - startTime > TimeSpan.FromSeconds(10) || totalEventsReceived >= 10)
                            {
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading from partition {partitionId}: {ex.Message}");
                    }

                    if (totalEventsReceived >= 10)
                    {
                        break;
                    }
                }

                Console.WriteLine($"Total events received: {totalEventsReceived}");
                Console.WriteLine("EventHubs operations completed successfully");

                // Clean up
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
    }
}