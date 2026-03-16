using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace Samples.AzureServiceBus.APM
{
    public enum TestMode
    {
        SendMessages,
        ReceiveMessages,
        ReceiveMessagesMultiple,
        ScheduleMessages,
        TestServiceBusMessageBatch
    }

    public class Program
    {
        private static readonly string ConnectionString = 
            Environment.GetEnvironmentVariable("ASB_CONNECTION_STRING") ??
            "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
        private const string QueueName = "samples-azureservicebus-queue";

        public static async Task Main(string[] args)
        {
            var testModeString = Environment.GetEnvironmentVariable("ASB_TEST_MODE");
            Console.WriteLine($"Starting Azure Service Bus APM Test Sample - Mode: {testModeString ?? "NONE"}");
            Console.WriteLine($"Connecting to: {ConnectionString}");
            Console.WriteLine($"Using queue: {QueueName}");

            try
            {
                var client = new ServiceBusClient(ConnectionString);
                Console.WriteLine("ServiceBus client created successfully");

                var sender = client.CreateSender(QueueName);
                var receiver = client.CreateReceiver(QueueName);
                Console.WriteLine("Sender and receiver created successfully");

                if (string.IsNullOrEmpty(testModeString) || !Enum.TryParse<TestMode>(testModeString, ignoreCase: true, out var testMode))
                {
                    var validModes = string.Join(", ", Enum.GetNames(typeof(TestMode)));
                    throw new ArgumentException($"Invalid or missing ASB_TEST_MODE environment variable. Expected one of: {validModes}. Got: '{testModeString}'");
                }

                switch (testMode)
                {
                    case TestMode.SendMessages:
                        await TestSendMessagesAsync(sender, receiver);
                        break;
                    case TestMode.ReceiveMessages:
                        await TestReceiveMessagesAsync(sender, receiver);
                        break;
                    case TestMode.ReceiveMessagesMultiple:
                        await TestReceiveMessagesMultipleAsync(sender, receiver);
                        break;
                    case TestMode.ScheduleMessages:
                        await TestScheduleMessagesAsync(sender);
                        break;
                    case TestMode.TestServiceBusMessageBatch:
                        await TestServiceBusMessageBatchAsync(sender, receiver);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(testMode), testMode, "Unhandled test mode");
                }

                await PurgeQueue(receiver);

                await sender.DisposeAsync();
                await receiver.DisposeAsync();
                await client.DisposeAsync();
                Console.WriteLine("Resources handled successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }

            Console.WriteLine("Azure Service Bus APM Test Sample completed successfully");
        }

        private static async Task TestSendMessagesAsync(ServiceBusSender sender, ServiceBusReceiver receiver)
        {
            Console.WriteLine("\n=== Send Messages Test ===");

            var testMessage = new ServiceBusMessage("Hello from SendMessages test!")
            {
                MessageId = Guid.NewGuid().ToString()
            };

            Console.WriteLine($"Sending single message with ID: {testMessage.MessageId}");
            await sender.SendMessageAsync(testMessage);
            Console.WriteLine("Single message sent successfully");

            var batchMessages = new ServiceBusMessage[3];
            for (int i = 0; i < batchMessages.Length; i++)
            {
                batchMessages[i] = new ServiceBusMessage($"Batch {i} Message from SendMessages test");
                Console.WriteLine($"Created batch {i} message with ID: {batchMessages[i].MessageId}");
            }

            Console.WriteLine("Sending multiple messages...");
            await sender.SendMessagesAsync(batchMessages);
            Console.WriteLine("Multiple messages sent successfully");
        }

        private static async Task TestReceiveMessagesAsync(ServiceBusSender sender, ServiceBusReceiver receiver)
        {
            Console.WriteLine("\n=== Receive Messages Test ===");

            // Send a test message first
            var testMessage = new ServiceBusMessage("Message for receive test")
            {
                MessageId = Guid.NewGuid().ToString()
            };
            await sender.SendMessageAsync(testMessage);
            Console.WriteLine($"Sent test message for receive with ID: {testMessage.MessageId}");

            // Receive the message
            Console.WriteLine("Attempting to receive message...");
            var receivedMessage = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10));

            if (receivedMessage != null)
            {
                Console.WriteLine($"Received message ID: {receivedMessage.MessageId}, Body: {receivedMessage.Body}");
                await receiver.CompleteMessageAsync(receivedMessage);
                Console.WriteLine("Message completed successfully");
            }
            else
            {
                Console.WriteLine("No message received within timeout");
            }
        }

        private static async Task TestReceiveMessagesMultipleAsync(ServiceBusSender sender, ServiceBusReceiver receiver)
        {
            Console.WriteLine("\n=== Receive Multiple Messages Test ===");

            var batchMessages = new ServiceBusMessage[3];
            for (int i = 0; i < batchMessages.Length; i++)
            {
                batchMessages[i] = new ServiceBusMessage($"Multi-receive test message {i}");
            }
            await sender.SendMessagesAsync(batchMessages);
            Console.WriteLine($"Sent {batchMessages.Length} test messages for multi-receive");

            Console.WriteLine("Attempting to receive multiple messages...");
            var receivedMessages = await receiver.ReceiveMessagesAsync(maxMessages: 3, maxWaitTime: TimeSpan.FromSeconds(10));

            Console.WriteLine($"Received {receivedMessages.Count} messages");
            foreach (var msg in receivedMessages)
            {
                Console.WriteLine($"Received message ID: {msg.MessageId}, Body: {msg.Body}");
                await receiver.CompleteMessageAsync(msg);
            }
            Console.WriteLine($"Completed {receivedMessages.Count} messages");
        }

        private static async Task TestScheduleMessagesAsync(ServiceBusSender sender)
        {
            Console.WriteLine("\n=== Schedule Messages Test ===");

            var scheduledMessages = new ServiceBusMessage[2];
            for (int i = 0; i < scheduledMessages.Length; i++)
            {
                scheduledMessages[i] = new ServiceBusMessage($"Scheduled Message {i} from ScheduleMessages test");
                Console.WriteLine($"Created scheduled message {i} with ID: {scheduledMessages[i].MessageId}");
            }

            Console.WriteLine("Scheduling multiple messages for future delivery...");
            var scheduleTime = DateTimeOffset.Now.AddSeconds(1);
            var sequenceNumbers = await sender.ScheduleMessagesAsync(scheduledMessages, scheduleTime);
            Console.WriteLine($"Scheduled {scheduledMessages.Length} messages for {scheduleTime}");
            Console.WriteLine($"Sequence numbers: {string.Join(", ", sequenceNumbers)}");

            // Wait for scheduled messages to be delivered so they can be purged
            // Calculate remaining time, but ensure we wait at least 2 seconds total to account for any delays
            var waitTime = scheduleTime - DateTimeOffset.Now;
            var totalWaitSeconds = Math.Max(2.0, waitTime.TotalSeconds + 1.0);
            Console.WriteLine($"Waiting {totalWaitSeconds:F1} seconds for scheduled messages to be delivered...");
            await Task.Delay(TimeSpan.FromSeconds(totalWaitSeconds));
            Console.WriteLine("Scheduled messages should now be delivered and ready for purging");
        }

        private static async Task TestServiceBusMessageBatchAsync(ServiceBusSender sender, ServiceBusReceiver receiver)
        {
            Console.WriteLine("\n=== Service Bus Message Batch Test ===");

            Console.WriteLine("Creating message batch...");
            using var messageBatch = await sender.CreateMessageBatchAsync();

            var messages = new[]
            {
                new ServiceBusMessage("Batch message 1")
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Subject = "BatchTest1"
                },
                new ServiceBusMessage("Batch message 2")
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Subject = "BatchTest2"
                },
                new ServiceBusMessage("Batch message 3")
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Subject = "BatchTest3"
                }
            };

            Console.WriteLine("Adding messages to batch using TryAddMessage...");
            for (int i = 0; i < messages.Length; i++)
            {
                var added = messageBatch.TryAddMessage(messages[i]);
                Console.WriteLine($"Message {i + 1} (ID: {messages[i].MessageId}) added to batch: {added}");
            }

            Console.WriteLine($"Batch now contains {messageBatch.Count} messages");

            Console.WriteLine("Sending message batch...");
            await sender.SendMessagesAsync(messageBatch);
            Console.WriteLine($"Successfully sent batch with {messageBatch.Count} messages");

            Console.WriteLine("Receiving messages from batch...");
            var receivedMessages = await receiver.ReceiveMessagesAsync(
                maxMessages: messageBatch.Count,
                maxWaitTime: TimeSpan.FromSeconds(10));

            Console.WriteLine($"Received {receivedMessages.Count} messages from batch");
            foreach (var msg in receivedMessages)
            {
                Console.WriteLine($"Processing message ID: {msg.MessageId}, Subject: {msg.Subject}, Body: {msg.Body}");
                await receiver.CompleteMessageAsync(msg);
            }
            Console.WriteLine($"Completed processing {receivedMessages.Count} messages");

            Console.WriteLine("Service Bus Message Batch test completed");
        }

        private static async Task PurgeQueue(ServiceBusReceiver receiver)
        {
            Console.WriteLine("Purging existing messages from queue...");
            var purgedCount = 0;

            try
            {
                while (true)
                {
                    var existingMessages = await receiver.ReceiveMessagesAsync(maxMessages: 10, maxWaitTime: TimeSpan.FromSeconds(2));
                    if (existingMessages == null || existingMessages.Count == 0)
                    {
                        break;
                    }

                    foreach (var msg in existingMessages)
                    {
                        await receiver.CompleteMessageAsync(msg);
                        purgedCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during queue purge (continuing anyway): {ex.Message}");
            }

            if (purgedCount > 0)
            {
                Console.WriteLine($"Purged {purgedCount} existing messages from queue");
            }
            else
            {
                Console.WriteLine("No existing messages found in queue");
            }
        }
    }
}
