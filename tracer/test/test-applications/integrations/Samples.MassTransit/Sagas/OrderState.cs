using MassTransit;

namespace Samples.MassTransit.Sagas;

public class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    
    public string CurrentState { get; set; } = string.Empty;
    
    public string? CustomerName { get; set; }
    
    public decimal TotalAmount { get; set; }
    
    public DateTime SubmittedAt { get; set; }
    
    public string? TransactionId { get; set; }
    
    public string? TrackingNumber { get; set; }
    
    public DateTime? CompletedAt { get; set; }
    
    public string? CancellationReason { get; set; }
    
    // For scheduled reminder
    public Guid? ReminderTokenId { get; set; }
}
