namespace Samples.MassTransit7.Contracts;

/// <summary>
/// Message to submit a new order - starts the saga
/// </summary>
public record OrderSubmitted
{
    public Guid OrderId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}

/// <summary>
/// Message to accept an order - transitions saga state
/// </summary>
public record OrderAccepted
{
    public Guid OrderId { get; init; }
}

/// <summary>
/// Message to complete an order - final state
/// </summary>
public record OrderCompleted
{
    public Guid OrderId { get; init; }
}

/// <summary>
/// Message that causes a saga to throw an exception - for testing exception handling in sagas
/// </summary>
public record OrderFailed
{
    public Guid OrderId { get; init; }
    public string Reason { get; init; } = string.Empty;
}
