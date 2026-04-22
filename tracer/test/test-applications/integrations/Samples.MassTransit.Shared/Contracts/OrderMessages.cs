namespace Samples.MassTransit.Contracts;

/// <summary>
/// Message to submit a new order - starts the saga
/// </summary>
public class OrderSubmitted
{
    public Guid OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

/// <summary>
/// Message to accept an order - transitions saga state
/// </summary>
public class OrderAccepted
{
    public Guid OrderId { get; set; }
}

/// <summary>
/// Message to complete an order - final state
/// </summary>
public class OrderCompleted
{
    public Guid OrderId { get; set; }
}

/// <summary>
/// Message that causes a saga to throw an exception - for testing exception handling in sagas
/// </summary>
public class OrderFailed
{
    public Guid OrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
