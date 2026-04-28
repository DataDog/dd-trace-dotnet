using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Samples;
using Samples.MassTransit;
using Samples.MassTransit.Contracts;
using Samples.MassTransit.Consumers;
using Samples.MassTransit8.Sagas;

var waitTimeout = TimeSpan.FromSeconds(30);

var transport = Environment.GetEnvironmentVariable("MASSTRANSIT_TRANSPORT")?.Trim().ToLowerInvariant();

// Back-compat with MASSTRANSIT_INMEMORY_ONLY=true
if (string.IsNullOrEmpty(transport)
    && string.Equals(Environment.GetEnvironmentVariable("MASSTRANSIT_INMEMORY_ONLY"), "true", StringComparison.OrdinalIgnoreCase))
{
    transport = "inmemory";
}

// Default (local dev): run every transport sequentially
if (string.IsNullOrEmpty(transport))
{
    transport = "all";
}

Console.WriteLine($"MassTransit 8 Sample - MASSTRANSIT_TRANSPORT={transport}");

switch (transport)
{
    case "inmemory":
        await RunInMemory();
        await RunInMemoryOnlyScenarios();
        break;
    case "rabbitmq":
        await RunRabbitMq();
        break;
    case "amazonsqs":
    case "sqs":
        await RunAmazonSqs();
        break;
    case "all":
        await RunInMemory();
        await TryRun(RunRabbitMq, "rabbitmq");
        await TryRun(RunAmazonSqs, "amazonsqs");
        await RunInMemoryOnlyScenarios();
        break;
    default:
        throw new InvalidOperationException($"Unknown MASSTRANSIT_TRANSPORT value: {transport}");
}

Console.WriteLine("All tests completed!");

async Task RunInMemory() =>
    await RunWithTransport<GettingStartedWithInMemory>(
        "inmemory",
        cfg =>
        {
            cfg.AddConsumer<GettingStartedWithInMemoryConsumer>();
            cfg.UsingInMemory((context, bus) => bus.ConfigureEndpoints(context));
        },
        value => new GettingStartedWithInMemory { Value = value },
        new Uri("queue:GettingStartedWithInMemory"));

async Task RunRabbitMq() =>
    await RunWithTransport<GettingStartedWithRabbitMq>(
        "rabbitmq",
        cfg =>
        {
            cfg.AddConsumer<GettingStartedWithRabbitMqConsumer>();
            cfg.UsingRabbitMq((context, bus) =>
            {
                var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
                bus.Host(rabbitHost, "/", h =>
                {
                    h.Username("guest");
                    h.Password("guest");
                });
                bus.ConfigureEndpoints(context);
            });
        },
        value => new GettingStartedWithRabbitMq { Value = value },
        new Uri("queue:GettingStartedWithRabbitMq"));

async Task RunAmazonSqs() =>
    await RunWithTransport<GettingStartedWithSqs>(
        "amazonsqs",
        cfg =>
        {
            cfg.AddConsumer<GettingStartedWithSqsConsumer>();
            cfg.UsingAmazonSqs((context, bus) =>
            {
                var localStackEndpoint = Environment.GetEnvironmentVariable("LOCALSTACK_ENDPOINT") ?? "http://localhost:4566";
                var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";

                bus.Host(region, h =>
                {
                    h.Config(new Amazon.SQS.AmazonSQSConfig { ServiceURL = localStackEndpoint });
                    h.Config(new Amazon.SimpleNotificationService.AmazonSimpleNotificationServiceConfig { ServiceURL = localStackEndpoint });
                    h.AccessKey("test");
                    h.SecretKey("test");
                });

                bus.ConfigureEndpoints(context);
            });
        },
        value => new GettingStartedWithSqs { Value = value },
        new Uri("queue:GettingStartedWithSqs"));

async Task RunInMemoryOnlyScenarios()
{
    await RunSagaTest();
    await RunExceptionTest();
    await RunHandlerExceptionTest();
    await RunSagaExceptionTest();
}

async Task TryRun(Func<Task> run, string transportName)
{
    try
    {
        await run();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{transportName}] Transport unavailable, skipping: {ex.GetType().Name}: {ex.Message}");
    }
}

async Task FlushTracerAsync(string scenarioName)
{
    Console.WriteLine($"[{scenarioName}] Flushing tracer...");
    await SampleHelpers.ForceTracerFlushAsync();
}

async Task RunWithTransport<TMessage>(
    string transportName,
    Action<IBusRegistrationConfigurator> configure,
    Func<string, TMessage> createMessage,
    Uri sendEndpointUri)
    where TMessage : class
{
    Console.WriteLine($"\n========== Testing {transportName.ToUpperInvariant()} ==========");

    var services = new ServiceCollection();
    services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

    services.AddMassTransit(configure);

    var serviceProvider = services.BuildServiceProvider();
    var busControl = serviceProvider.GetRequiredService<IBusControl>();

    try
    {
        Console.WriteLine($"[{transportName}] Starting the bus...");
        await busControl.StartAsync();

        // Publish (fanout to all subscribers)
        var publishValue = $"Hello via Publish from {transportName} at {DateTimeOffset.Now}";
        Console.WriteLine($"[{transportName}] Publishing message (Publish)...");
        await busControl.Publish(createMessage(publishValue));
        await TestSignal.WaitAsync(publishValue, waitTimeout);

        // Send (direct to specific endpoint)
        var sendValue = $"Hello via Send from {transportName} at {DateTimeOffset.Now}";
        Console.WriteLine($"[{transportName}] Sending message (Send)...");
        var sendEndpoint = await busControl.GetSendEndpoint(sendEndpointUri);
        await sendEndpoint.Send(createMessage(sendValue));
        await TestSignal.WaitAsync(sendValue, waitTimeout);
        await FlushTracerAsync(transportName);

        Console.WriteLine($"[{transportName}] Test completed successfully!");
    }
    finally
    {
        Console.WriteLine($"[{transportName}] Stopping the bus...");
        await busControl.StopAsync();
        await FlushTracerAsync($"{transportName}-shutdown");
    }
}

async Task RunSagaTest()
{
    Console.WriteLine("\n========== Testing SAGA STATE MACHINE ==========");

    var services = new ServiceCollection();
    services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

    services.AddMassTransit(x =>
    {
        x.AddSagaStateMachine<OrderStateMachine, OrderState>()
            .InMemoryRepository();

        x.UsingInMemory((context, cfg) =>
        {
            cfg.ConfigureEndpoints(context);
        });
    });

    var serviceProvider = services.BuildServiceProvider();
    var busControl = serviceProvider.GetRequiredService<IBusControl>();

    try
    {
        Console.WriteLine("[saga] Starting the bus...");
        await busControl.StartAsync();

        var orderId = Guid.NewGuid();
        Console.WriteLine($"[saga] Testing order saga with OrderId: {orderId}");

        Console.WriteLine("[saga] Publishing OrderSubmitted event...");
        await busControl.Publish(new OrderSubmitted
        {
            OrderId = orderId,
            CustomerName = "Test Customer",
            Amount = 99.99m
        });
        await TestSignal.WaitAsync($"saga:submitted:{orderId}", waitTimeout);

        Console.WriteLine("[saga] Publishing OrderAccepted event...");
        await busControl.Publish(new OrderAccepted { OrderId = orderId });
        await TestSignal.WaitAsync($"saga:accepted:{orderId}", waitTimeout);

        Console.WriteLine("[saga] Publishing OrderCompleted event...");
        await busControl.Publish(new OrderCompleted { OrderId = orderId });
        await TestSignal.WaitAsync($"saga:completed:{orderId}", waitTimeout);
        await FlushTracerAsync("saga");

        Console.WriteLine("[saga] Saga test completed successfully!");
    }
    finally
    {
        Console.WriteLine("[saga] Stopping the bus...");
        await busControl.StopAsync();
        await FlushTracerAsync("saga-shutdown");
    }
}

async Task RunExceptionTest()
{
    Console.WriteLine("\n========== Testing CONSUMER EXCEPTION HANDLING ==========");

    var services = new ServiceCollection();
    services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

    services.AddMassTransit(x =>
    {
        x.AddConsumer<FailingConsumer>();

        x.UsingInMemory((context, cfg) =>
        {
            cfg.ConfigureEndpoints(context);
        });
    });

    var serviceProvider = services.BuildServiceProvider();
    var busControl = serviceProvider.GetRequiredService<IBusControl>();
    var faultSignalObserver = new FaultSignalObserver(new Dictionary<Type, string>
    {
        [typeof(Fault<FailingMessage>)] = "fault:consumer-exception"
    });

    try
    {
        Console.WriteLine("[consumer-exception] Starting the bus...");
        await busControl.StartAsync();
        var publishObserverHandle = busControl.ConnectPublishObserver(faultSignalObserver);
        var sendObserverHandle = busControl.ConnectSendObserver(faultSignalObserver);

        const string failingValue = "Consumer failure test";
        Console.WriteLine("[consumer-exception] Publishing message that will cause an exception...");
        await busControl.Publish(new FailingMessage { Value = failingValue });
        await TestSignal.WaitAsync("fault:consumer-exception", waitTimeout);
        await FlushTracerAsync("consumer-exception");

        Console.WriteLine("[consumer-exception] Consumer exception test completed - check traces for error spans!");

        publishObserverHandle.Disconnect();
        sendObserverHandle.Disconnect();
    }
    finally
    {
        Console.WriteLine("[consumer-exception] Stopping the bus...");
        await busControl.StopAsync();
        await FlushTracerAsync("consumer-exception-shutdown");
    }
}

async Task RunHandlerExceptionTest()
{
    Console.WriteLine("\n========== Testing HANDLER EXCEPTION HANDLING ==========");
    Console.WriteLine("[handler-exception] NOTE: Handlers use a different Activity than Consumers - testing for gaps");

    var services = new ServiceCollection();
    services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

    services.AddMassTransit(x =>
    {
        x.UsingInMemory((context, cfg) =>
        {
            cfg.ReceiveEndpoint("handler-failing", e =>
            {
                e.Handler<HandlerFailingMessage>(async ctx =>
                {
                    Console.WriteLine($"[handler-exception] Handler received: {ctx.Message.Value} - About to throw exception");
                    TestSignal.Set(ctx.Message.Value);
                    await Task.CompletedTask;
                    throw new InvalidOperationException($"Handler failure test: {ctx.Message.Value}");
                });
            });
        });
    });

    var serviceProvider = services.BuildServiceProvider();
    var busControl = serviceProvider.GetRequiredService<IBusControl>();
    var faultSignalObserver = new FaultSignalObserver(new Dictionary<Type, string>
    {
        [typeof(Fault<HandlerFailingMessage>)] = "fault:handler-exception"
    });

    try
    {
        Console.WriteLine("[handler-exception] Starting the bus...");
        await busControl.StartAsync();
        var publishObserverHandle = busControl.ConnectPublishObserver(faultSignalObserver);
        var sendObserverHandle = busControl.ConnectSendObserver(faultSignalObserver);

        const string handlerFailureValue = "Handler will fail";
        Console.WriteLine("[handler-exception] Sending message to handler that will throw...");
        var sendEndpoint = await busControl.GetSendEndpoint(new Uri("loopback://localhost/handler-failing"));
        await sendEndpoint.Send(new HandlerFailingMessage { Value = handlerFailureValue });
        await TestSignal.WaitAsync("fault:handler-exception", waitTimeout);
        await FlushTracerAsync("handler-exception");

        Console.WriteLine("[handler-exception] Handler exception test completed - check traces for error spans!");

        publishObserverHandle.Disconnect();
        sendObserverHandle.Disconnect();
    }
    finally
    {
        Console.WriteLine("[handler-exception] Stopping the bus...");
        await busControl.StopAsync();
        await FlushTracerAsync("handler-exception-shutdown");
    }
}

async Task RunSagaExceptionTest()
{
    Console.WriteLine("\n========== Testing SAGA EXCEPTION HANDLING ==========");
    Console.WriteLine("[saga-exception] NOTE: Sagas use their own Activity - testing for gaps");

    var services = new ServiceCollection();
    services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

    services.AddMassTransit(x =>
    {
        x.AddSagaStateMachine<OrderStateMachine, OrderState>()
            .InMemoryRepository();

        x.UsingInMemory((context, cfg) =>
        {
            cfg.ConfigureEndpoints(context);
        });
    });

    var serviceProvider = services.BuildServiceProvider();
    var busControl = serviceProvider.GetRequiredService<IBusControl>();
    var faultSignalObserver = new FaultSignalObserver(new Dictionary<Type, string>
    {
        [typeof(Fault<OrderFailed>)] = "fault:saga-exception"
    });

    try
    {
        Console.WriteLine("[saga-exception] Starting the bus...");
        await busControl.StartAsync();
        var publishObserverHandle = busControl.ConnectPublishObserver(faultSignalObserver);
        var sendObserverHandle = busControl.ConnectSendObserver(faultSignalObserver);

        var orderId = Guid.NewGuid();
        Console.WriteLine($"[saga-exception] Testing saga exception with OrderId: {orderId}");

        Console.WriteLine("[saga-exception] Publishing OrderSubmitted event...");
        await busControl.Publish(new OrderSubmitted
        {
            OrderId = orderId,
            CustomerName = "Exception Test Customer",
            Amount = 99.99m
        });
        await TestSignal.WaitAsync($"saga:submitted:{orderId}", waitTimeout);

        Console.WriteLine("[saga-exception] Publishing OrderFailed event (will cause saga to throw)...");
        await busControl.Publish(new OrderFailed
        {
            OrderId = orderId,
            Reason = "Intentional saga failure for testing"
        });
        await TestSignal.WaitAsync("fault:saga-exception", waitTimeout);
        await FlushTracerAsync("saga-exception");

        Console.WriteLine("[saga-exception] Saga exception test completed - check traces for error spans!");

        publishObserverHandle.Disconnect();
        sendObserverHandle.Disconnect();
    }
    finally
    {
        Console.WriteLine("[saga-exception] Stopping the bus...");
        await busControl.StopAsync();
        await FlushTracerAsync("saga-exception-shutdown");
    }
}
