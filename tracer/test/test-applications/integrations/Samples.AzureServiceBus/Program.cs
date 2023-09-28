using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus.Administration;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Samples.AzureServiceBus
{
    public class Program
    {
        // Before executing the program, ensure the sessions, topics, and subscriptions exist
        private static readonly RequestHelper _requestHelper = new();
        private static readonly string QueueName = "samples-azureservicebus-queue";
        private static readonly string QueueNameSessionEnabled = "samples-azureservicebus-queue-session";
        private static readonly string TopicName = "samples-azureservicebus-topic";

        private static async Task Main(string[] args)
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

                // Test the ServiceBusProcessor API by sending one message and consuming it via the ProcessMessageAsync callback
                await _requestHelper.TestServiceBusProcessorAsync(tracer, QueueName);

                // Test the ServiceBusSessionReceiver API
                await _requestHelper.TestServiceBusSessionReceiverAsync(tracer, QueueNameSessionEnabled);

                // Test the ServiceBusSender API: Scheduled messages
                await _requestHelper.TestSenderSchedulingAsync(tracer, QueueName);

                // Test the ServiceBusSender and ServiceBusReceiver API's: Single messages
                await _requestHelper.TestServiceBusReceiverIndividualMessageAsync(tracer, QueueName);

                // Test the ServiceBusSender and ServiceBusReceiver API's: Batch messages
                await _requestHelper.TestServiceBusReceiverBatchMessagesAsync(tracer, QueueName);

                // Test sending to topics and receiving from subscriptions, instead of operating directly with queues
                await _requestHelper.TestServiceBusSubscriptionProcessorAsync(tracer, TopicName, new[] { "subscription1", "subscription2", "subscription3" });
            }
            finally
            {
                await DisposeServiceBusAsync();
            }
        }

        private static async Task InitializeServiceBusAsync()
        {
            // Create queue
            await _requestHelper.AdminClient.CreateQueueAsync(QueueName);
            await _requestHelper.AdminClient.CreateQueueAsync(new CreateQueueOptions(QueueNameSessionEnabled) { RequiresSession = true });

            // Create topics
            await _requestHelper.AdminClient.CreateTopicAsync(TopicName);

            // Create subscriptions
            await _requestHelper.AdminClient.CreateSubscriptionAsync(TopicName, "subscription1");
            await _requestHelper.AdminClient.CreateSubscriptionAsync(TopicName, "subscription2");
            await _requestHelper.AdminClient.CreateSubscriptionAsync(TopicName, "subscription3");
        }

        private static async Task DisposeServiceBusAsync()
        {
            await _requestHelper.AdminClient.DeleteQueueAsync(QueueName); // Delete so each test is self-contained
            await _requestHelper.AdminClient.DeleteQueueAsync(QueueNameSessionEnabled); // Delete so each test is self-contained
            await _requestHelper.AdminClient.DeleteTopicAsync(TopicName); // Delete so each test is self-contained

            await _requestHelper.DisposeAsync();
        }
    }
}
