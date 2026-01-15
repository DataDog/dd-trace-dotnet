using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Samples.MassTransit7.Consumers;
using Samples.MassTransit7.Messages;
using Samples.MassTransit7.Sagas;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ProcessPaymentConsumer>();
    x.AddConsumer<ShipOrderConsumer>();

    x.AddSagaStateMachine<OrderStateMachine, OrderState>()
        .InMemoryRepository();

    x.UsingInMemory((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
await host.StartAsync();

var bus = host.Services.GetRequiredService<IBus>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

try
{
    // Saga workflow: OrderSubmitted -> ProcessPayment -> PaymentProcessed -> ShipOrder -> OrderShipped -> OrderCompleted
    logger.LogInformation("Starting saga workflow test");
    await bus.Publish(new OrderSubmitted(Guid.NewGuid(), "Test Customer", 99.99m, DateTime.UtcNow));
    await Task.Delay(3000); // Wait for saga to complete
    logger.LogInformation("Test completed");
}
finally
{
    await host.StopAsync();
}
