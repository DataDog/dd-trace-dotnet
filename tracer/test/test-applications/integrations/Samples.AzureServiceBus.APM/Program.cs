using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace Samples.AzureServiceBus.APM
{
    public class Program
    {
        private static readonly string ConnectionString = 
            Environment.GetEnvironmentVariable("ASB_CONNECTION_STRING") ??
            "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
        private const string QueueName = "samples-azureservicebus-queue";

        public static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Azure Service Bus APM Test Sample");
            Console.WriteLine($"Connecting to: {ConnectionString}");
            Console.WriteLine($"Using queue: {QueueName}");

            try
            {
                var client = new ServiceBusClient(ConnectionString);
                Console.WriteLine("ServiceBus client created successfully");

                var sender = client.CreateSender(QueueName);
                var receiver = client.CreateReceiver(QueueName);
                Console.WriteLine("Sender and receiver created successfully");

                Console.WriteLine("\n=== Single Message ===");
                var testMessage = new ServiceBusMessage("Hello from APM test!")
                {
                    MessageId = Guid.NewGuid().ToString()
                };
                
                Console.WriteLine($"Sending message with ID: {testMessage.MessageId}");
                await sender.SendMessageAsync(testMessage);
                Console.WriteLine("Single message sent successfully");

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

                Console.WriteLine("\n=== Multiple Messages ===");
                
                var batchMessages = new ServiceBusMessage[3];
                for (int i = 0; i < batchMessages.Length; i++)
                {
                    batchMessages[i] = new ServiceBusMessage($"Batch {i} Message");
                    Console.WriteLine($"Created batch {i} message with ID: {batchMessages[i].MessageId}");
                }
                
                Console.WriteLine("Sending multiple messages...");
                await sender.SendMessagesAsync(batchMessages);
                Console.WriteLine("Multiple messages sent successfully");

                Console.WriteLine("Attempting to receive messages...");
                var receivedMessages = await receiver.ReceiveMessagesAsync(maxMessages: 3, maxWaitTime: TimeSpan.FromSeconds(10));
                
                Console.WriteLine($"Received {receivedMessages.Count} messages");
                foreach (var msg in receivedMessages)
                {
                    Console.WriteLine($"Received message ID: {msg.MessageId}, Body: {msg.Body}");
                    await receiver.CompleteMessageAsync(msg);
                }
                Console.WriteLine($"Completed {receivedMessages.Count} messages");

                Console.WriteLine("\n=== Message Batch ===");

                var messageBatch = await sender.CreateMessageBatchAsync();
                Console.WriteLine("Created message batch");

                var batchMessage1 = new ServiceBusMessage("Batch Message 1") { MessageId = Guid.NewGuid().ToString() };
                var batchMessage2 = new ServiceBusMessage("Batch Message 2") { MessageId = Guid.NewGuid().ToString() };
                var batchMessage3 = new ServiceBusMessage("Batch Message 3") { MessageId = Guid.NewGuid().ToString() };

                Console.WriteLine($"Adding message 1 to batch (ID: {batchMessage1.MessageId})");
                var added1 = messageBatch.TryAddMessage(batchMessage1);
                Console.WriteLine($"Message 1 added to batch: {added1}");

                Console.WriteLine($"Adding message 2 to batch (ID: {batchMessage2.MessageId})");
                var added2 = messageBatch.TryAddMessage(batchMessage2);
                Console.WriteLine($"Message 2 added to batch: {added2}");

                Console.WriteLine($"Adding message 3 to batch (ID: {batchMessage3.MessageId})");
                var added3 = messageBatch.TryAddMessage(batchMessage3);
                Console.WriteLine($"Message 3 added to batch: {added3}");

                Console.WriteLine($"Batch contains {messageBatch.Count} messages, Size: {messageBatch.SizeInBytes} bytes");

                Console.WriteLine("Sending message batch...");
                await sender.SendMessagesAsync(messageBatch);
                Console.WriteLine("Message batch sent successfully");

                Console.WriteLine("Attempting to receive batch messages...");
                var batchReceivedMessages = await receiver.ReceiveMessagesAsync(maxMessages: 3, maxWaitTime: TimeSpan.FromSeconds(10));

                Console.WriteLine($"Received {batchReceivedMessages.Count} batch messages");
                foreach (var msg in batchReceivedMessages)
                {
                    Console.WriteLine($"Received batch message ID: {msg.MessageId}, Body: {msg.Body}");
                    await receiver.CompleteMessageAsync(msg);
                }
                Console.WriteLine($"Completed {batchReceivedMessages.Count} batch messages");

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
    }
}
