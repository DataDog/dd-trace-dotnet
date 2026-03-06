namespace Samples.MassTransit7.Contracts;

/// <summary>
/// Message to submit a new order - starts the saga
/// </summary>
public record OrderSubmitted
{
    public Guid OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

/// <summary>
/// Message to accept an order - transitions saga state
/// </summary>
public record OrderAccepted
{
    public Guid OrderId { get; set; }
}

/// <summary>
/// Message to complete an order - final state
/// </summary>
public record OrderCompleted
{
    public Guid OrderId { get; set; }
}

/// <summary>
/// Message that causes a saga to throw an exception - for testing exception handling in sagas
/// </summary>
public record OrderFailed
{
    public Guid OrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
