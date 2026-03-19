using Automatonymous;

namespace Samples.MassTransit7.Sagas;

/// <summary>
/// Saga instance that holds the state of an order
/// </summary>
public class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }

    // Use State type for proper state machine integration
    public State? CurrentState { get; set; }

    public string? CustomerName { get; set; }
    public decimal Amount { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
