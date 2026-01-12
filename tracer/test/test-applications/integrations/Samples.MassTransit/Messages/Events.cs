namespace Samples.MassTransit.Messages;

// Event messages - Notify that something happened
public record OrderSubmitted(Guid OrderId, string CustomerName, decimal TotalAmount, DateTime SubmittedAt);

public record PaymentProcessed(Guid OrderId, bool Success, string? TransactionId);

public record PaymentFailed(Guid OrderId, string Reason);

public record OrderShipped(Guid OrderId, string TrackingNumber, DateTime ShippedAt);

public record OrderCompleted(Guid OrderId, DateTime CompletedAt);

public record OrderCancelled(Guid OrderId, string Reason, DateTime CancelledAt);
