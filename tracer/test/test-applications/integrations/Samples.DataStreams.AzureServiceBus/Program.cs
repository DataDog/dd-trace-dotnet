using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Samples.AzureServiceBus;

namespace Samples.DataStreams.AzureServiceBus
{
    public static class Program
    {
        private static readonly string ConnectionString = Environment.GetEnvironmentVariable("ASB_CONNECTION_STRING");
        private static readonly ServiceBusClient Client = new(ConnectionString);
        private static readonly ServiceBusAdministrationClient AdminClient = new(ConnectionString);

        private static readonly string QueueName = "dsm-direct-queue";

        private static readonly string TopicWithFiltersName = "dsm-topic-subscription-filters";
        private static readonly string Topic2Name = "dsm-topic2";
        private static readonly string Topic3Name = "dsm-topic3";

        private static readonly string DefaultSubscription = "subscription";
        private static readonly string SubjectFilterValue = "order";

        public static async Task Main(string[] args)
        {
            var serviceName = Environment.GetEnvironmentVariable("DD_SERVICE") ?? "Samples.AzureServiceBus";
            var serviceVersion = Environment.GetEnvironmentVariable("DD_VERSION") ?? "1.0.0";
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(
                    ResourceBuilder.CreateDefault()
                        .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
                .AddSource(serviceName)
                .AddAzureServiceBusIfEnvironmentVariablePresent()
                .AddOtlpExporterIfEnvironmentVariablePresent()
                .AddConsoleExporter()
                .Build();

            var tracer = tracerProvider.GetTracer(serviceName);
            try
            {
                await InitializeServiceBusAsync();

                // This should create 3 separate Azure Service Bus pipelines:
                // 1. Publish directly to queue:
                //    (direction:out, topic:dsm-direct-queue) -> (direction:in, topic:dsm-direct-queue)
                // 2. Publish to topic, read from multiple subscriptions:
                //    (direction:out, topic:dsm-topic-subscription-filters) ->
                //        (direction:in, topic:dsm-topic-subscription-filters/Subscriptions/subscription1)
                //        (direction:in, topic:dsm-topic-subscription-filters/Subscriptions/subscription2)
                // 3. Publish to topics/subscriptions in series
                //    (direction:out, topic:dsm-topic2)
                //       -> (direction:in, topic:dsm-topic2/Subscriptions/subscription)
                //         -> (direction:out, topic:dsm-topic3)
                //           -> (direction:in, topic:dsm-topic3/Subscriptions/subscription)
                //
                // In total there are 4 Send operations and 5 Consume operations

                // Set up senders
                await using var queueSender = Client.CreateSender(QueueName);
                await using var topicSender = Client.CreateSender(TopicWithFiltersName);
                await using var topic2Sender = Client.CreateSender(Topic2Name);
                await using var topic3Sender = Client.CreateSender(Topic3Name);

                // Pipeline 1: Publish to queue
                var messageBatch = await queueSender.CreateMessageBatchAsync();
                messageBatch.TryAddMessage(CreateMessage("message"));
                await queueSender.SendMessagesAsync(messageBatch);
                await CreateQueueProcessor(QueueName);

                // Pipeline 2: Publish to topic with multiple subscriptions (Queue=dsm-direct-queue, Subject=order)
                var message2 = CreateMessage("message2");
                message2.Subject = "order";
                await topicSender.SendMessageAsync(message2);

                await CreateTopicProcessor(TopicWithFiltersName, new[] { "subscription1", "subscription2" });

                // Pipeline 3: Publish to topic which daisy chains topics and subscriptions (Queue=dsm-direct-queue, Subject=account)
                var message3 = CreateMessage("message3");
                message3.Subject = "account";
                await topic2Sender.SendMessagesAsync(new List<ServiceBusMessage> { message3 });

                await CreateTopicProcessor(Topic2Name, new[] { DefaultSubscription }, topic3Sender);
                await CreateTopicProcessor(Topic3Name, new[] { DefaultSubscription });
            }
            finally
            {
                // Dispose everything
                await DisposeServiceBusAsync();
            }
        }

        private static async Task InitializeServiceBusAsync()
        {
            // Create queue
            await AdminClient.CreateQueueAsync(QueueName);

            // Create topics
            await AdminClient.CreateTopicAsync(TopicWithFiltersName);
            await AdminClient.CreateTopicAsync(Topic2Name);
            await AdminClient.CreateTopicAsync(Topic3Name);

            // Create subscriptions
            await AdminClient.CreateSubscriptionAsync(
                new CreateSubscriptionOptions(TopicWithFiltersName, "subscription1"),
                new CreateRuleOptions("typeFilter", new CorrelationRuleFilter() { Subject = SubjectFilterValue }));

            await AdminClient.CreateSubscriptionAsync(
                new CreateSubscriptionOptions(TopicWithFiltersName, "subscription2"),
                new CreateRuleOptions("typeFilter", new CorrelationRuleFilter() { Subject = SubjectFilterValue }));

            await AdminClient.CreateSubscriptionAsync(Topic2Name, DefaultSubscription);
            await AdminClient.CreateSubscriptionAsync(Topic3Name, DefaultSubscription);
        }

        private static async Task CreateQueueProcessor(string queueName, ServiceBusSender sender = null)
        {
            var processor = Client.CreateProcessor(queueName);
            var processorTcs = new TaskCompletionSource<bool>();

            processor.ProcessMessageAsync += HandleAndProduce(queueName, processorTcs, sender);
            processor.ProcessErrorAsync += ProcessError(queueName);
            await processor.StartProcessingAsync();

            // To ensure stable ordering, we'll await one message from the newly established subscriber before starting the next
            await processorTcs.Task;
            await processor.StopProcessingAsync();
            await processor.DisposeAsync();
        }

        private static async Task CreateTopicProcessor(string topicName, string[] subscriptionNames, ServiceBusSender sender = null)
        {
            for (int i = 0; i < subscriptionNames.Length; i++)
            {
                var subscriptionName = subscriptionNames[i];
                var processor = Client.CreateProcessor(topicName, subscriptionName);
                var processorTcs = new TaskCompletionSource<bool>();

                var displayName = $"{topicName}:{subscriptionName}";
                processor.ProcessMessageAsync += HandleAndProduce(displayName, processorTcs, sender);
                processor.ProcessErrorAsync += ProcessError(displayName);
                await processor.StartProcessingAsync();

                // To ensure stable ordering, we'll await one message from the newly established subscriber before starting the next
                await processorTcs.Task;
                await processor.StopProcessingAsync();
                await processor.DisposeAsync();
            }
        }

        private static Func<ProcessMessageEventArgs, Task> HandleAndProduce(string displayName, TaskCompletionSource<bool> processOneTcs, ServiceBusSender sender = null)
            => async (args) =>
            {
                string body = args.Message.Body.ToString();
                Console.WriteLine($"[{displayName}] Received: {body}");

                // complete the message. message is deleted from the queue.
                await args.CompleteMessageAsync(args.Message);

                if (sender is not null)
                {
                    await sender.SendMessageAsync(CreateMessage($"Forwarded from {displayName}"));
                }

                // We only wait for one send span, so immediately set the Result of the processing TaskCompletionSource
                processOneTcs.SetResult(true);
            };

        private static Func<ProcessErrorEventArgs, Task> ProcessError(string displayName)
            => (args) =>
            {
                Console.WriteLine($"[{displayName}] {args.Exception}");
                return Task.CompletedTask;
            };

        private static ServiceBusMessage CreateMessage(string body) => new ServiceBusMessage(body)
        {
            ContentType = "text/plain",
            ReplyTo = "replyto",
            Subject = "subject",
        };

        private static async Task DisposeServiceBusAsync()
        {
            await AdminClient.DeleteQueueAsync(QueueName); // Delete so each test is self-contained
            await AdminClient.DeleteTopicAsync(TopicWithFiltersName); // Delete so each test is self-contained
            await AdminClient.DeleteTopicAsync(Topic2Name); // Delete so each test is self-contained
            await AdminClient.DeleteTopicAsync(Topic3Name); // Delete so each test is self-contained

            await Client.DisposeAsync();
        }
    }
}
