using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using OpenTelemetry.Trace;

namespace Samples.AzureServiceBus
{
    // ServiceBusProcessor API (obtained from Client.CreateProcessor())
    //
    // Require testing:
    // - processor.StartProcessingAsync
    // - processor.StopProcessingAsync
    //
    // Do not require testing:
    // - processor.CloseAsync
    // - processor.DisposeAsync
    // - processor.UpdateConcurrency
    // - processor.UpdatePrefetchCount
    partial class RequestHelper
    {
        public async Task TestServiceBusProcessorAsync(Tracer tracer, string queueName)
        {
            await SendMessageToProcessorAsync(tracer, queueName, resourceName: "SendMessageToProcessorAsync");

            await using var processor = Client.CreateProcessor(queueName);
            var processorTcs = new TaskCompletionSource<bool>();

            processor.ProcessMessageAsync += ProcessMessageHandler("QueueProcessor", processorTcs);
            processor.ProcessErrorAsync += ProcessErrorHandler("QueueProcessor");
            await processor.StartProcessingAsync();

            await processorTcs.Task;
            await processor.StopProcessingAsync();
            await processor.DisposeAsync();
        }

        public async Task TestServiceBusSubscriptionProcessorAsync(Tracer tracer, string topicName, string[] subscriptionNames)
        {
            await SendMessageToProcessorAsync(tracer, topicName, resourceName: "SendMessageToTopicAsync");

            List<ServiceBusProcessor> processorList = new();

            for (int i = 0; i < subscriptionNames.Length; i++)
            {
                var processor = Client.CreateProcessor(topicName, subscriptionNames[i]);
                var processorTcs = new TaskCompletionSource<bool>();

                processorList.Add(processor);

                processor.ProcessMessageAsync += ProcessMessageHandler(subscriptionNames[i], processorTcs);
                processor.ProcessErrorAsync += ProcessErrorHandler(subscriptionNames[i]);
                await processor.StartProcessingAsync();

                // To ensure stable ordering, we'll await one message from the newly established subscriber before starting the next
                await processorTcs.Task;
                await processor.StopProcessingAsync();
                await processor.DisposeAsync();
            }
        }

        private Func<ProcessMessageEventArgs, Task> ProcessMessageHandler(string processorName, TaskCompletionSource<bool> processOneTcs)
            => async (args) =>
            {
                string body = args.Message.Body.ToString();
                Console.WriteLine($"[{processorName}] Received: {body}");

                // complete the message. message is deleted from the queue.
                await args.CompleteMessageAsync(args.Message);

                // We only wait for one send span, so immediately set the Result of the processing TaskCompletionSource
                processOneTcs.SetResult(true);
            };

        private Func<ProcessErrorEventArgs, Task> ProcessErrorHandler(string processorName)
            => (args) =>
            {
                Console.WriteLine($"[{processorName}] {args.Exception}");
                return Task.CompletedTask;
            };

        private async Task SendMessageToProcessorAsync(Tracer tracer, string queueOrTopicName, string resourceName)
        {
            using var span = tracer.StartActiveSpan(resourceName);

            await using var sender = Client.CreateSender(queueOrTopicName);
            await sender.SendMessageAsync(CreateMessage(resourceName));
        }
    }
}
