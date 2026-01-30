using Automatonymous;
using Samples.MassTransit7.Contracts;

namespace Samples.MassTransit7.Sagas;

/// <summary>
/// State machine that manages the order lifecycle
/// </summary>
public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public OrderStateMachine()
    {
        // Define states
        InstanceState(x => x.CurrentState);

        // Define events with correlation by OrderId
        Event(() => OrderSubmitted, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => OrderAccepted, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => OrderCompleted, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => OrderFailed, x => x.CorrelateById(context => context.Message.OrderId));

        // Initial state: when order is submitted
        Initially(
            When(OrderSubmitted)
                .Then(context =>
                {
                    Console.WriteLine($"[Saga] Order {context.Data.OrderId} submitted by {context.Data.CustomerName}");
                    context.Instance.CustomerName = context.Data.CustomerName;
                    context.Instance.Amount = context.Data.Amount;
                    context.Instance.SubmittedAt = DateTime.UtcNow;
                })
                .TransitionTo(Submitted));

        // Submitted state: waiting for acceptance or failure
        During(Submitted,
            When(OrderAccepted)
                .Then(context =>
                {
                    Console.WriteLine($"[Saga] Order {context.Data.OrderId} accepted");
                    context.Instance.AcceptedAt = DateTime.UtcNow;
                })
                .TransitionTo(Accepted),
            When(OrderFailed)
                .Then(context =>
                {
                    Console.WriteLine($"[Saga] Order {context.Data.OrderId} FAILING with reason: {context.Data.Reason}");
                    // Intentionally throw an exception to test saga exception handling
                    throw new InvalidOperationException($"Saga failure test: {context.Data.Reason}");
                }));

        // Accepted state: waiting for completion
        During(Accepted,
            When(OrderCompleted)
                .Then(context =>
                {
                    Console.WriteLine($"[Saga] Order {context.Data.OrderId} completed");
                    context.Instance.CompletedAt = DateTime.UtcNow;
                })
                .TransitionTo(Completed)
                .Finalize());

        // Clean up completed sagas
        SetCompletedWhenFinalized();
    }

    // States
    public State Submitted { get; private set; } = null!;
    public State Accepted { get; private set; } = null!;
    public State Completed { get; private set; } = null!;

    // Events
    public Event<OrderSubmitted> OrderSubmitted { get; private set; } = null!;
    public Event<OrderAccepted> OrderAccepted { get; private set; } = null!;
    public Event<OrderCompleted> OrderCompleted { get; private set; } = null!;
    public Event<OrderFailed> OrderFailed { get; private set; } = null!;
}
