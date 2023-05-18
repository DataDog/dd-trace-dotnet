using System;
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
        private readonly TaskCompletionSource<bool> _processorTcs = new();

        public async Task TestServiceBusProcessorAsync(Tracer tracer, string queueName)
        {
            await SendMessageToProcessorAsync(tracer, queueName);

            var processor = Client.CreateProcessor(queueName);
            processor.ProcessMessageAsync += ProcessMessageHandler;
            processor.ProcessErrorAsync += ProcessErrorHandler;
            await processor.StartProcessingAsync();

            await _processorTcs.Task;
            await processor.StopProcessingAsync();
        }

        private async Task ProcessMessageHandler(ProcessMessageEventArgs args)
        {
            string body = args.Message.Body.ToString();
            Console.WriteLine($"Received: {body}");

            // complete the message. message is deleted from the queue.
            await args.CompleteMessageAsync(args.Message);

            // We only wait for one send span, so immediately set the Result of the processing TaskCompletionSource
            _processorTcs.SetResult(true);
        }

        private Task ProcessErrorHandler(ProcessErrorEventArgs args)
        {
            Console.WriteLine(args.Exception.ToString());
            return Task.CompletedTask;
        }

        private async Task SendMessageToProcessorAsync(Tracer tracer, string queueName)
        {
            using var span = tracer.StartActiveSpan("SendMessageToProcessorAsync");

            var sender = Client.CreateSender(queueName);
            await sender.SendMessageAsync(CreateMessage("SendMessageToProcessorAsync"));
        }
    }
}
