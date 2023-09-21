using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Azure.Amqp.Framing;
using OpenTelemetry.Trace;

namespace Samples.AzureServiceBus
{
    partial class RequestHelper
    {
        // ServiceBusSender API (obtained from Client.CreateSender())
        // sender.CancelScheduledMessageAsync
        // sender.CancelScheduledMessagesAsync
        // sender.CreateMessageBatchAsync
        // sender.ScheduleMessageAsync
        // sender.ScheduleMessagesAsync
        // sender.SendMessageAsync
        // sender.SendMessagesAsync

        // ServiceBusRceiver API (obtained from Client.CreateReceiver())
        // receiver.AbandonMessageAsync
        // receiver.CompleteMessageAsync
        // receiver.DeadLetterMessageAsync
        // receiver.DeferMessageAsync
        // receiver.PeekMessageAsync
        // receiver.PeekMessagesAsync
        // receiver.ReceiveDeferredMessageAsync
        // receiver.ReceiveDeferredMessagesAsync
        // receiver.ReceiveMessageAsync
        // receiver.ReceiveMessagesAsync
        // receiver.RenewMessageLockAsync

        private readonly string ConnectionString = Environment.GetEnvironmentVariable("ASB_CONNECTION_STRING");

        public RequestHelper()
        {
            AdminClient = new ServiceBusAdministrationClient(ConnectionString);
            Client = new ServiceBusClient(ConnectionString);
            IsRunningInAzure = ConnectionString.Contains("servicebus.windows.net");
        }

        internal ServiceBusAdministrationClient AdminClient { get; }

        internal ServiceBusClient Client { get; }

        private bool IsRunningInAzure { get; }

        public async Task TestSenderSchedulingAsync(Tracer tracer, string queueName)
        {
            if (!IsRunningInAzure)
            {
                Console.WriteLine("Skipping TestSenderSchedulingAsync. The program is not configured to run against a full-featured Azure Service Bus.");
                return;
            }

            using var span = tracer.StartActiveSpan(nameof(TestSenderSchedulingAsync));

            await using var sender = Client.CreateSender(queueName);
            var sequenceNumber = await sender.ScheduleMessageAsync(CreateMessage("ScheduleMessagesAndCancelAsync"), DateTimeOffset.Now.AddMinutes(10));
            await sender.CancelScheduledMessageAsync(sequenceNumber);

            List<ServiceBusMessage> messagesList = new()
            {
                CreateMessage("ScheduleMessagesAndCancelAsync-List-1"),
                CreateMessage("ScheduleMessagesAndCancelAsync-List-2"),
                CreateMessage("ScheduleMessagesAndCancelAsync-List-3"),
            };
            var sequenceNumbers = await sender.ScheduleMessagesAsync(messagesList, DateTimeOffset.Now.AddMinutes(10));
            await sender.CancelScheduledMessagesAsync(sequenceNumbers);
        }

        public async Task TestServiceBusReceiverIndividualMessageAsync(Tracer tracer, string queueName)
        {
            using (var span = tracer.StartActiveSpan("SendIndividualMessageAsync"))
            {
                await using var sender = Client.CreateSender(queueName);
                await sender.SendMessageAsync(CreateMessage("HandleIndividualMessageAsync"));
            }

            await using var receiver = Client.CreateReceiver(queueName, new ServiceBusReceiverOptions()
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });

            // With the live message, perform the following steps:
            // - Send message
            // - Peek
            // - Receive
            // - Renew
            // - Abandon
            await receiver.PeekMessageAsync();
            var message = await receiver.ReceiveMessageAsync();
            await receiver.RenewMessageLockAsync(message); // TODO: Check if the emulator can do this
            await receiver.AbandonMessageAsync(message);

            // With the live message, perform the following steps:
            // - Receive
            // - Defer
            // - ReceiveDeferred
            // - DeadLetter
            message = await receiver.ReceiveMessageAsync();

            if (IsRunningInAzure)
            {
                await receiver.DeferMessageAsync(message);
                message = await receiver.ReceiveDeferredMessageAsync(message.SequenceNumber);
            }

            await receiver.DeadLetterMessageAsync(message);

            // With the now-deadlettered message:
            // - Receive
            // - Complete
            await using var deadLetterReceiver = Client.CreateReceiver(GetDeadLetterQueueName(queueName), new ServiceBusReceiverOptions());
            message = await deadLetterReceiver.ReceiveMessageAsync();
            await deadLetterReceiver.CompleteMessageAsync(message);
        }

        public async Task TestServiceBusReceiverBatchMessagesAsync(Tracer tracer, string queueName)
        {
            await using var sender = Client.CreateSender(queueName);

            using (tracer.StartActiveSpan("SendBatchMessagesAsync - IEnumerable_ServiceBusMessage"))
            {
                List<ServiceBusMessage> messagesList = new()
                {
                    CreateMessage("HandleBatchMessageAsync-List-1"),
                    CreateMessage("HandleBatchMessageAsync-List-2"),
                    CreateMessage("HandleBatchMessageAsync-List-3"),
                };
                await sender.SendMessagesAsync(messagesList);
            }

            using (tracer.StartActiveSpan("SendBatchMessagesAsync - ServiceBusMessageBatch"))
            {
                var serviceBusMessageBatch = await sender.CreateMessageBatchAsync();
                serviceBusMessageBatch.TryAddMessage(CreateMessage("HandleBatchMessageAsync-Batch-1"));
                serviceBusMessageBatch.TryAddMessage(CreateMessage("HandleBatchMessageAsync-Batch-2"));
                serviceBusMessageBatch.TryAddMessage(CreateMessage("HandleBatchMessageAsync-Batch-3"));

                await sender.SendMessagesAsync(serviceBusMessageBatch);
            }

            await using var receiver = Client.CreateReceiver(queueName, new ServiceBusReceiverOptions()
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });

            var peekedMessages = await receiver.PeekMessagesAsync(maxMessages: 3);

            await ReceiveMessagesAsync(receiver, maxMessages: 3, getMessagesAsync: ReceiveMessagesWithMaxMessagesAsync);
            await ReceiveMessagesAsync(receiver, maxMessages: 3, getMessagesAsync: ReceiveMessagesWithEnumerableAsync);
        }

        // With the message batch, perform the following steps:
        // - Receive
        // - Defer (individually)
        // - ReceiveDeferred
        // - Complete  (individually)
        private async Task ReceiveMessagesAsync(ServiceBusReceiver receiver, int maxMessages, Func<ServiceBusReceiver, int, Task<IEnumerable<ServiceBusReceivedMessage>>> getMessagesAsync)
        {
            List<long> deferredSequenceNumbers = new();
            var receivedMessages = await getMessagesAsync(receiver, maxMessages);

            if (!IsRunningInAzure)
            {
                return;
            }

            foreach (var message in receivedMessages)
            {
                deferredSequenceNumbers.Add(message.SequenceNumber);
                await receiver.DeferMessageAsync(message);
            }

            foreach (var receivedMessage in await receiver.ReceiveDeferredMessagesAsync(deferredSequenceNumbers))
            {
                await receiver.CompleteMessageAsync(receivedMessage);
            }
        }

        private async Task<IEnumerable<ServiceBusReceivedMessage>> ReceiveMessagesWithMaxMessagesAsync(ServiceBusReceiver receiver, int maxMessages)
        {
            var receivedMessages = await receiver.ReceiveMessagesAsync(maxMessages);
            if (!IsRunningInAzure)
            {
                foreach (var message in receivedMessages)
                {
                    await receiver.CompleteMessageAsync(message);
                }
            }

            return receivedMessages;
        }

        private async Task<IEnumerable<ServiceBusReceivedMessage>> ReceiveMessagesWithEnumerableAsync(ServiceBusReceiver receiver, int maxMessages)
        {
            int messagesReceived = 0;
            List<ServiceBusReceivedMessage> receivedMessages = new();

            try
            {
                CancellationTokenSource receiveMessageCts = new CancellationTokenSource();
            
                await foreach (var message in receiver.ReceiveMessagesAsync(receiveMessageCts.Token))
                {
                    receivedMessages.Add(message);
                    
                    if (!IsRunningInAzure)
                    {
                        var bodyString = message.Body.ToString();
                        await receiver.CompleteMessageAsync(message);
                    }

                    messagesReceived++;
                    if (messagesReceived == maxMessages)
                    {
                        receiveMessageCts.Cancel();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Do nothing
            }

            return receivedMessages;
        }

        public async Task DisposeAsync()
        {
            await Client.DisposeAsync();
        }

        private string GetDeadLetterQueueName(string queueName) =>
          IsRunningInAzure switch
          {
              true => $"{queueName}/$deadletterqueue",
              false => $"{queueName}-dlq",
          };

        private ServiceBusMessage CreateMessage(string body) => new ServiceBusMessage(body)
        {
            ContentType = "text/plain",
            ReplyTo = "replyto",
            Subject = "subject",
        };
    }
}
