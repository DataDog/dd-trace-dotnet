using MassTransit;

namespace Samples.MassTransit8.Sagas;

/// <summary>
/// Saga instance that holds the state of an order
/// </summary>
public class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }

    // In MT8, state is stored as a string
    public string? CurrentState { get; set; }

    public string? CustomerName { get; set; }
    public decimal Amount { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
