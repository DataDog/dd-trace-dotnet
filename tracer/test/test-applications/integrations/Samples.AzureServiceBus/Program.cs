using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Samples.AzureServiceBus
{
    public class Program
    {
        // Before executing the program, ensure the sessions, topics, and subscriptions exist
        private static readonly RequestHelper _requestHelper = new();
        private static readonly string _sessionDisabledQueueName = "samples-azureservicebus-queue";
        private static readonly string _sessionEnabledQueueName = "samples-azureservicebus-queue-session";
        private static readonly string _topicName = "samples-azureservicebus-topic";
        private static readonly string _subscriptionPrefix = "subscription";
        private static readonly int _numSubscribers = 3;

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

            // Test the ServiceBusProcessor API by sending one message and consuming it via the ProcessMessageAsync callback
            await _requestHelper.TestServiceBusProcessorAsync(tracer, _sessionDisabledQueueName);

            // Test the ServiceBusSessionReceiver API
            await _requestHelper.TestServiceBusSessionReceiverAsync(tracer, _sessionEnabledQueueName);

            // Test the ServiceBusSender API: Scheduled messages
            await _requestHelper.TestSenderSchedulingAsync(tracer, _sessionDisabledQueueName);

            // Test the ServiceBusSender and ServiceBusReceiver API's: Single messages
            await _requestHelper.TestServiceBusReceiverIndividualMessageAsync(tracer, _sessionDisabledQueueName);

            // Test the ServiceBusSender and ServiceBusReceiver API's: Batch messages
            await _requestHelper.TestServiceBusReceiverBatchMessagesAsync(tracer, _sessionDisabledQueueName);

            // Test sending to topics and receiving from subscriptions, instead of operating directly with queues
            await _requestHelper.TestServiceBusSubscriptionProcessorAsync(tracer, _topicName, _subscriptionPrefix, _numSubscribers);

            await _requestHelper.DisposeAsync();
        }
    }
}
