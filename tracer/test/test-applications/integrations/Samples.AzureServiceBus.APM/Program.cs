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
                var multipleMessages = new[]
                {
                    new ServiceBusMessage("Message 1") { MessageId = Guid.NewGuid().ToString() },
                    new ServiceBusMessage("Message 2") { MessageId = Guid.NewGuid().ToString() },
                    new ServiceBusMessage("Message 3") { MessageId = Guid.NewGuid().ToString() }
                };
                
                Console.WriteLine("Sending 3 messages...");
                await sender.SendMessagesAsync(multipleMessages);
                Console.WriteLine("Multiple messages sent successfully");

                // Receive multiple messages
                Console.WriteLine("Attempting to receive messages...");
                var receivedMessages = await receiver.ReceiveMessagesAsync(maxMessages: 3, maxWaitTime: TimeSpan.FromSeconds(10));
                
                Console.WriteLine($"Received {receivedMessages.Count} messages");
                foreach (var msg in receivedMessages)
                {
                    Console.WriteLine($"Received message ID: {msg.MessageId}, Body: {msg.Body}");
                    await receiver.CompleteMessageAsync(msg);
                }
                Console.WriteLine($"Completed {receivedMessages.Count} messages");

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
