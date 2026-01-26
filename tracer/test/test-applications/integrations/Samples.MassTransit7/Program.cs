using MassTransit;
using Samples.MassTransit7;
using Samples.MassTransit7.Consumers;

// Transport selection via environment variable: "rabbitmq" (default), "amazonsqs", or "azureservicebus"
var transport = Environment.GetEnvironmentVariable("MASSTRANSIT_TRANSPORT")?.ToLowerInvariant() ?? "rabbitmq";

Console.WriteLine($"MassTransit 7 Sample - Using transport: {transport}");

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddMassTransit(x =>
        {
            x.AddConsumer<GettingStartedConsumer>();

            switch (transport)
            {
                case "amazonsqs":
                    ConfigureAmazonSqs(x);
                    break;
                case "azureservicebus":
                    ConfigureAzureServiceBus(x);
                    break;
                case "rabbitmq":
                default:
                    ConfigureRabbitMq(x);
                    break;
            }
        });

        services.AddHostedService<Worker>();
    });

var host = builder.Build();
await host.RunAsync();

void ConfigureRabbitMq(IBusRegistrationConfigurator x)
{
    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ConfigureEndpoints(context);
    });
}

void ConfigureAmazonSqs(IBusRegistrationConfigurator x)
{
    x.UsingAmazonSqs((context, cfg) =>
    {
        // Use LocalStack for local testing (default endpoint)
        // Set LOCALSTACK_ENDPOINT to override (e.g., "http://localhost:4566")
        var localStackEndpoint = Environment.GetEnvironmentVariable("LOCALSTACK_ENDPOINT") ?? "http://localhost:4566";
        var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";

        cfg.Host(region, h =>
        {
            // Configure SQS client for LocalStack
            h.Config(new Amazon.SQS.AmazonSQSConfig
            {
                ServiceURL = localStackEndpoint
            });

            // Configure SNS client for LocalStack (MassTransit uses SNS for pub/sub topics)
            h.Config(new Amazon.SimpleNotificationService.AmazonSimpleNotificationServiceConfig
            {
                ServiceURL = localStackEndpoint
            });

            // LocalStack doesn't require real credentials
            h.AccessKey("test");
            h.SecretKey("test");
        });

        cfg.ConfigureEndpoints(context);
    });
}

void ConfigureAzureServiceBus(IBusRegistrationConfigurator x)
{
    x.UsingAzureServiceBus((context, cfg) =>
    {
        // Azure Service Bus connection string from environment variable
        // Default uses Azure Service Bus Emulator (https://learn.microsoft.com/en-us/azure/service-bus-messaging/test-locally-with-service-bus-emulator)
        var connectionString = Environment.GetEnvironmentVariable("AZURE_SERVICEBUS_CONNECTION_STRING")
            ?? "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

        cfg.Host(connectionString);

        cfg.ConfigureEndpoints(context);
    });
}
