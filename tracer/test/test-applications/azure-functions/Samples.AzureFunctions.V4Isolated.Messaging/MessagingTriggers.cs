using System.Diagnostics;
using System.Net;
using System.Text;
using Azure;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Samples;

namespace Samples.AzureFunctions.Messaging;

public class MessagingTriggers
{
    private const string ServiceBusQueueName = "samples-azureservicebus-queue";
    private const string EventHubName = "samples-eventhubs-hub";
    private const string EventHubConsumerGroup = "cg1";
    private const string TestIdEnvironmentVariable = "DD_AZURE_FUNCTIONS_MESSAGING_TEST_ID";
    private const string TestIdApplicationProperty = "dd-test-id";
    private static int _shutdownStarted;

    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<MessagingTriggers> _logger;

    public MessagingTriggers(ILogger<MessagingTriggers> logger, IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _lifetime = lifetime;
    }

    [Function("ServiceBusTrigger")]
    public Task ServiceBusTrigger(
        [ServiceBusTrigger(ServiceBusQueueName, Connection = "ASB_CONNECTION_STRING")] ServiceBusReceivedMessage message)
    {
        using (SampleHelpers.CreateScope("Manual inside ServiceBusTrigger"))
        {
            _logger.LogInformation(
                "Processed Service Bus message {MessageId}: {Body}",
                message.MessageId,
                message.Body.ToString());
        }

        ScheduleShutdown();
        return Task.CompletedTask;
    }

    [Function("EventHubTrigger")]
    public Task EventHubTrigger(
        [EventHubTrigger(EventHubName, Connection = "EVENTHUBS_CONNECTION_STRING", ConsumerGroup = EventHubConsumerGroup)] EventData[] events)
    {
        var eventData = events.First();
        using (SampleHelpers.CreateScope("Manual inside EventHubTrigger"))
        {
            _logger.LogInformation(
                "Processed Event Hubs event {MessageId}: {Body}",
                eventData.MessageId,
                Encoding.UTF8.GetString(eventData.EventBody.ToArray()));
        }

        ScheduleShutdown();
        return Task.CompletedTask;
    }

    [Function("EventGridTrigger")]
    public Task EventGridTrigger([EventGridTrigger] CloudEvent cloudEvent)
    {
        using (SampleHelpers.CreateScope("Manual inside EventGridTrigger"))
        {
            _logger.LogInformation(
                "Processed Event Grid event {Id}: {Type}",
                cloudEvent.Id,
                cloudEvent.Type);
        }

        ScheduleShutdown();
        return Task.CompletedTask;
    }

    [Function("SeedServiceBusMessage")]
    public async Task<HttpResponseData> SeedServiceBusMessage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "seed/servicebus")] HttpRequestData req)
    {
        var testId = Environment.GetEnvironmentVariable(TestIdEnvironmentVariable) ?? string.Empty;
        await using var client = new ServiceBusClient(Environment.GetEnvironmentVariable("ASB_CONNECTION_STRING")!);
        await using var sender = client.CreateSender(ServiceBusQueueName);
        var message = new ServiceBusMessage($"Seeded message {testId}")
        {
            MessageId = testId,
            Subject = nameof(ServiceBusTrigger),
        };
        message.ApplicationProperties[TestIdApplicationProperty] = testId;
        await sender.SendMessageAsync(message);
        return req.CreateResponse(HttpStatusCode.OK);
    }

    [Function("SeedEventHubEvent")]
    public async Task<HttpResponseData> SeedEventHubEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "seed/eventhub")] HttpRequestData req)
    {
        var testId = Environment.GetEnvironmentVariable(TestIdEnvironmentVariable) ?? string.Empty;
        await using var producer = new EventHubProducerClient(Environment.GetEnvironmentVariable("EVENTHUBS_CONNECTION_STRING")!, EventHubName);
        var eventData = new EventData(Encoding.UTF8.GetBytes($"Seeded event {testId}"))
        {
            MessageId = testId,
            ContentType = nameof(EventHubTrigger),
        };
        eventData.Properties[TestIdApplicationProperty] = testId;
        await producer.SendAsync(new[] { eventData });
        return req.CreateResponse(HttpStatusCode.OK);
    }

    [Function("SeedEventGridEvent")]
    public async Task<HttpResponseData> SeedEventGridEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "seed/eventgrid")] HttpRequestData req)
    {
        var testId = Environment.GetEnvironmentVariable(TestIdEnvironmentVariable) ?? string.Empty;
        var endpoint = Environment.GetEnvironmentVariable("EVENTGRID_TOPIC_ENDPOINT")!;

        // Publish to the Event Grid emulator's topic endpoint (EVENTGRID_TOPIC_ENDPOINT). The producer
        // instrumentation creates the azure_eventgrid.send span and injects the W3C trace context into the
        // CloudEvent's extension attributes. The emulator then forwards the event to the function host's
        // webhook — preserving the CloudEvent (and its traceparent) and adding the "aeg-event-type:
        // Notification" header the webhook requires — so the receive span can link back to this send.
        var client = new EventGridPublisherClient(new Uri(endpoint), new AzureKeyCredential("test-key"));

        var cloudEvent = new CloudEvent(
            source: "/Samples.AzureFunctions.Messaging/eventgrid",
            type: nameof(EventGridTrigger),
            jsonSerializableData: new { message = $"Seeded event {testId}", testId })
        {
            Id = testId,
        };
        await client.SendEventAsync(cloudEvent);
        return req.CreateResponse(HttpStatusCode.OK);
    }

    [Function("EventGridOutputBinding")]
    [EventGridOutput(TopicEndpointUri = "EVENTGRID_TOPIC_ENDPOINT", TopicKeySetting = "EVENTGRID_TOPIC_KEY")]
    public CloudEvent EventGridOutputBinding(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "output/eventgrid")] HttpRequestData req)
    {
        var testId = Environment.GetEnvironmentVariable(TestIdEnvironmentVariable) ?? string.Empty;
        return new CloudEvent(
            source: "/Samples.AzureFunctions.Messaging/eventgrid-output-binding",
            type: nameof(EventGridTrigger),
            jsonSerializableData: new { message = $"Output binding event {testId}", testId })
        {
            Id = testId,
        };
    }

    private void ScheduleShutdown()
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            await SampleHelpers.ForceTracerFlushAsync();
            _logger.LogInformation("Stopping Azure Functions host");
            _lifetime.StopApplication();

            foreach (var process in Process.GetProcessesByName("func"))
            {
                _logger.LogInformation("Killing {Pid} ({Name})", process.Id, process.ProcessName);
                process.Kill();
            }
        });
    }
}
