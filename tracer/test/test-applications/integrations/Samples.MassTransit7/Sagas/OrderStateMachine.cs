using Automatonymous;
using MassTransit;
using Microsoft.Extensions.Logging;
using Samples.MassTransit7.Messages;

namespace Samples.MassTransit7.Sagas;

public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public OrderStateMachine(ILogger<OrderStateMachine> logger)
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderSubmitted, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentProcessed, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => OrderShipped, x => x.CorrelateById(m => m.Message.OrderId));

        Initially(
            When(OrderSubmitted)
                .Then(context =>
                {
                    context.Instance.CustomerName = context.Data.CustomerName;
                    context.Instance.TotalAmount = context.Data.TotalAmount;
                    logger.LogInformation("Saga: Order {OrderId} received", context.Instance.CorrelationId);
                })
                .Publish(context => new ProcessPayment(context.Instance.CorrelationId, context.Instance.TotalAmount))
                .TransitionTo(AwaitingPayment));

        During(AwaitingPayment,
            When(PaymentProcessed)
                .Then(context => logger.LogInformation("Saga: Payment received for Order {OrderId}", context.Instance.CorrelationId))
                .Publish(context => new ShipOrder(context.Instance.CorrelationId, $"{context.Instance.CustomerName}'s Address"))
                .TransitionTo(AwaitingShipment));

        During(AwaitingShipment,
            When(OrderShipped)
                .Then(context => logger.LogInformation("Saga: Order {OrderId} shipped", context.Instance.CorrelationId))
                .Publish(context => new OrderCompleted(context.Instance.CorrelationId, DateTime.UtcNow))
                .Finalize());

        SetCompletedWhenFinalized();
    }

    public State AwaitingPayment { get; private set; } = null!;
    public State AwaitingShipment { get; private set; } = null!;

    public Event<OrderSubmitted> OrderSubmitted { get; private set; } = null!;
    public Event<PaymentProcessed> PaymentProcessed { get; private set; } = null!;
    public Event<OrderShipped> OrderShipped { get; private set; } = null!;
}
