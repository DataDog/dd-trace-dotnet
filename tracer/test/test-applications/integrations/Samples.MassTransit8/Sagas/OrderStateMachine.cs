using MassTransit;
using Samples.MassTransit8.Contracts;

namespace Samples.MassTransit8.Sagas;

/// <summary>
/// State machine that manages the order lifecycle (MT8 version)
/// In MT8, MassTransitStateMachine is built into MassTransit (no separate Automatonymous package)
/// </summary>
public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public OrderStateMachine()
    {
        // Define states - in MT8, state is stored as string by default
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
                    Console.WriteLine($"[Saga] Order {context.Message.OrderId} submitted by {context.Message.CustomerName}");
                    context.Saga.CustomerName = context.Message.CustomerName;
                    context.Saga.Amount = context.Message.Amount;
                    context.Saga.SubmittedAt = DateTime.UtcNow;
                })
                .TransitionTo(Submitted));

        // Submitted state: waiting for acceptance or failure
        During(Submitted,
            When(OrderAccepted)
                .Then(context =>
                {
                    Console.WriteLine($"[Saga] Order {context.Message.OrderId} accepted");
                    context.Saga.AcceptedAt = DateTime.UtcNow;
                })
                .TransitionTo(Accepted),
            When(OrderFailed)
                .Then(context =>
                {
                    Console.WriteLine($"[Saga] Order {context.Message.OrderId} FAILING with reason: {context.Message.Reason}");
                    // Intentionally throw an exception to test saga exception handling
                    throw new InvalidOperationException($"Saga failure test: {context.Message.Reason}");
                }));

        // Accepted state: waiting for completion
        During(Accepted,
            When(OrderCompleted)
                .Then(context =>
                {
                    Console.WriteLine($"[Saga] Order {context.Message.OrderId} completed");
                    context.Saga.CompletedAt = DateTime.UtcNow;
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
