using Automatonymous;

namespace Samples.MassTransit7.Sagas;

public class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string? CurrentState { get; set; }
    public string? CustomerName { get; set; }
    public decimal TotalAmount { get; set; }
}
