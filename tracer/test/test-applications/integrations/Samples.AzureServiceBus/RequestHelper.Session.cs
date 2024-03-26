using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Amqp.Framing;
using OpenTelemetry.Trace;

namespace Samples.AzureServiceBus
{
    // ServiceBusSessionReceiver API (obtained from Client.AcceptSessionAsync() and Client.AcceptNextSessionAsync())
    // sessionReceiver.GetSessionStateAsync
    // sessionReceiver.RenewSessionLockAsync
    // sessionReceiver.SetSessionStateAsync
    partial class RequestHelper
    {
        private readonly TaskCompletionSource<bool> _sessionProcessorTcs = new();

        public async Task TestServiceBusSessionReceiverAsync(Tracer tracer, string queueName)
        {
            if (!IsRunningInAzure)
            {
                Console.WriteLine($"Skipping {nameof(TestServiceBusSessionReceiverAsync)}. The program is not configured to run against a full-featured Azure Service Bus.");
                return;
            }

            await SendSessionMessagesAsync(tracer, queueName);

            // Create a session receiver for both sessions
            await using ServiceBusSessionReceiver firstSessionReceiver = await Client.AcceptSessionAsync(queueName, "FirstSessionId");
            await using ServiceBusSessionReceiver secondSessionReceiver = await Client.AcceptSessionAsync(queueName, "SecondSessionId");

            // Receive messages for first session
            var firstSessionMessages = await firstSessionReceiver.ReceiveMessagesAsync(maxMessages: 2);
            foreach (var firstSessionMessage in firstSessionMessages)
            {
                Console.WriteLine($"firstSessionReceiver.ReceiveMessagesAsync: Received message with SessionId={firstSessionMessage.SessionId}, Body={firstSessionMessage.Body}");
                await firstSessionReceiver.CompleteMessageAsync(firstSessionMessage);
            }

            // Call ServiceBusSessionReceiver API's for the second session receiver
            await secondSessionReceiver.RenewSessionLockAsync();
            await secondSessionReceiver.SetSessionStateAsync(new BinaryData("SecondSessionId State"));
            var state = await secondSessionReceiver.GetSessionStateAsync();
            Console.WriteLine($"secondSessionReceiver.GetSessionStateAsync: Received SessionState={state}");

            // Receive message for second session
            ServiceBusReceivedMessage secondSessionMessage = await secondSessionReceiver.ReceiveMessageAsync();
            Console.WriteLine($"secondSessionReceiver.ReceiveMessagesAsync: Received message with SessionId={secondSessionMessage.SessionId}, Body={secondSessionMessage.Body}");
            await secondSessionReceiver.CompleteMessageAsync(secondSessionMessage);

            // Process messages for processor session
            var sessionProcessOptions = new ServiceBusSessionProcessorOptions();
            sessionProcessOptions.SessionIds.Add("ProcessorSessionId");

            await using var processor = Client.CreateSessionProcessor(queueName, sessionProcessOptions);
            processor.ProcessMessageAsync += ProcessMessageHandler;
            processor.ProcessErrorAsync += ProcessErrorHandler("QueueSessionProcessor");
            await processor.StartProcessingAsync();

            await _sessionProcessorTcs.Task;
            await processor.StopProcessingAsync();
        }

        private async Task SendSessionMessagesAsync(Tracer tracer, string queueName)
        {
            await using ServiceBusSender sender = Client.CreateSender(queueName);

            // Create and send messages for one session
            using (var span = tracer.StartActiveSpan("FirstSessionId - Producer"))
            {
                ServiceBusMessage message = CreateMessage("FirstSessionId - Message #1");
                message.SessionId = "FirstSessionId";
                await sender.SendMessageAsync(message);

                message = CreateMessage("FirstSessionId - Message #2");
                message.SessionId = "FirstSessionId";
                await sender.SendMessageAsync(message);
            }

            // Create and send messages for another session
            using (var span = tracer.StartActiveSpan("SecondSessionId - Producer"))
            {
                ServiceBusMessage message = CreateMessage("SecondSessionId - Message #1");
                message.SessionId = "SecondSessionId";
                await sender.SendMessageAsync(message);
            }

            // Create and send message for the processor session
            using (var span = tracer.StartActiveSpan("ProcessorSessionId - Producer"))
            {
                ServiceBusMessage message = CreateMessage("ProcessorSession - Message #1");
                message.SessionId = "ProcessorSessionId";
                await sender.SendMessageAsync(message);
            }
        }

        private async Task ProcessMessageHandler(ProcessSessionMessageEventArgs args)
        {
            string body = args.Message.Body.ToString();
            Console.WriteLine($"Received: {body}");

            // complete the message. message is deleted from the queue.
            await args.CompleteMessageAsync(args.Message);

            // We only wait for one send span, so immediately set the Result of the processing TaskCompletionSource
            _sessionProcessorTcs.SetResult(true);
        }
    }
}
