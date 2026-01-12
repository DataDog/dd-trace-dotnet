using MassTransit;
using Microsoft.Extensions.Logging;
using Samples.MassTransit.Messages;

namespace Samples.MassTransit.Sagas;

public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public OrderStateMachine(ILogger<OrderStateMachine> logger)
    {
        InstanceState(x => x.CurrentState);

        // Define events
        Event(() => OrderSubmitted, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentProcessed, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentFailed, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => OrderShipped, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => OrderCancelled, x => x.CorrelateById(m => m.Message.OrderId));

        // Define the workflow
        Initially(
            When(OrderSubmitted)
                .Then(context =>
                {
                    context.Saga.CustomerName = context.Message.CustomerName;
                    context.Saga.TotalAmount = context.Message.TotalAmount;
                    context.Saga.SubmittedAt = context.Message.SubmittedAt;
                    logger.LogInformation("Order {OrderId} entered state machine for {Customer}",
                        context.Saga.CorrelationId,
                        context.Message.CustomerName);
                })
                .Publish(context => new ProcessPayment(context.Saga.CorrelationId, context.Saga.TotalAmount))
                .TransitionTo(AwaitingPayment));

        During(AwaitingPayment,
            When(PaymentProcessed)
                .If(context => context.Message.Success, x => x
                    .Then(context =>
                    {
                        context.Saga.TransactionId = context.Message.TransactionId;
                        logger.LogInformation("Payment successful for Order {OrderId}, TransactionId={TransactionId}",
                            context.Saga.CorrelationId,
                            context.Message.TransactionId);
                    })
                    .Publish(context => new ShipOrder(
                        context.Saga.CorrelationId,
                        $"{context.Saga.CustomerName}'s Address"))
                    .TransitionTo(AwaitingShipment)),
            When(PaymentFailed)
                .Then(context =>
                {
                    context.Saga.CancellationReason = $"Payment failed: {context.Message.Reason}";
                    logger.LogWarning("Payment failed for Order {OrderId}: {Reason}",
                        context.Saga.CorrelationId,
                        context.Message.Reason);
                })
                .Publish(context => new OrderCancelled(
                    context.Saga.CorrelationId,
                    context.Saga.CancellationReason!,
                    DateTime.UtcNow))
                .TransitionTo(Cancelled));

        During(AwaitingShipment,
            When(OrderShipped)
                .Then(context =>
                {
                    context.Saga.TrackingNumber = context.Message.TrackingNumber;
                    context.Saga.CompletedAt = DateTime.UtcNow;
                    logger.LogInformation("Order {OrderId} shipped with tracking {TrackingNumber}",
                        context.Saga.CorrelationId,
                        context.Message.TrackingNumber);
                })
                .Publish(context => new OrderCompleted(
                    context.Saga.CorrelationId,
                    context.Saga.CompletedAt!.Value))
                .Finalize());

        During(Cancelled,
            When(OrderCancelled)
                .Then(context =>
                {
                    logger.LogInformation("Order {OrderId} cancelled: {Reason}",
                        context.Saga.CorrelationId,
                        context.Message.Reason);
                })
                .Finalize());

        // Handle completion
        SetCompletedWhenFinalized();
    }

    public State AwaitingPayment { get; private set; } = null!;
    public State AwaitingShipment { get; private set; } = null!;
    public State Cancelled { get; private set; } = null!;

    public Event<OrderSubmitted> OrderSubmitted { get; private set; } = null!;
    public Event<PaymentProcessed> PaymentProcessed { get; private set; } = null!;
    public Event<PaymentFailed> PaymentFailed { get; private set; } = null!;
    public Event<OrderShipped> OrderShipped { get; private set; } = null!;
    public Event<OrderCancelled> OrderCancelled { get; private set; } = null!;
}
