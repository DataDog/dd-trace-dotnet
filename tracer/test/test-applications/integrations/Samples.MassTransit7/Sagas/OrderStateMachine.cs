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
                    context.Instance.CustomerName = context.Data.CustomerName;
                    context.Instance.TotalAmount = context.Data.TotalAmount;
                    context.Instance.SubmittedAt = context.Data.SubmittedAt;
                    logger.LogInformation("Order {OrderId} entered state machine for {Customer}",
                        context.Instance.CorrelationId,
                        context.Data.CustomerName);
                })
                .Publish(context => new ProcessPayment(context.Instance.CorrelationId, context.Instance.TotalAmount))
                .TransitionTo(AwaitingPayment));

        During(AwaitingPayment,
            When(PaymentProcessed)
                .If(context => context.Data.Success, x => x
                    .Then(context =>
                    {
                        context.Instance.TransactionId = context.Data.TransactionId;
                        logger.LogInformation("Payment successful for Order {OrderId}, TransactionId={TransactionId}",
                            context.Instance.CorrelationId,
                            context.Data.TransactionId);
                    })
                    .Publish(context => new ShipOrder(
                        context.Instance.CorrelationId,
                        $"{context.Instance.CustomerName}'s Address"))
                    .TransitionTo(AwaitingShipment)),
            When(PaymentFailed)
                .Then(context =>
                {
                    context.Instance.CancellationReason = $"Payment failed: {context.Data.Reason}";
                    logger.LogWarning("Payment failed for Order {OrderId}: {Reason}",
                        context.Instance.CorrelationId,
                        context.Data.Reason);
                })
                .Publish(context => new OrderCancelled(
                    context.Instance.CorrelationId,
                    context.Instance.CancellationReason!,
                    DateTime.UtcNow))
                .TransitionTo(Cancelled));

        During(AwaitingShipment,
            When(OrderShipped)
                .Then(context =>
                {
                    context.Instance.TrackingNumber = context.Data.TrackingNumber;
                    context.Instance.CompletedAt = DateTime.UtcNow;
                    logger.LogInformation("Order {OrderId} shipped with tracking {TrackingNumber}",
                        context.Instance.CorrelationId,
                        context.Data.TrackingNumber);
                })
                .Publish(context => new OrderCompleted(
                    context.Instance.CorrelationId,
                    context.Instance.CompletedAt!.Value))
                .Finalize());

        During(Cancelled,
            When(OrderCancelled)
                .Then(context =>
                {
                    logger.LogInformation("Order {OrderId} cancelled: {Reason}",
                        context.Instance.CorrelationId,
                        context.Data.Reason);
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
