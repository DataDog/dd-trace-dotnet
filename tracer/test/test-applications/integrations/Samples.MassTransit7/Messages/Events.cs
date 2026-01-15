namespace Samples.MassTransit7.Messages;

public record OrderSubmitted(Guid OrderId, string CustomerName, decimal TotalAmount, DateTime SubmittedAt);

public record PaymentProcessed(Guid OrderId, bool Success, string? TransactionId);

public record OrderShipped(Guid OrderId, string TrackingNumber, DateTime ShippedAt);

public record OrderCompleted(Guid OrderId, DateTime CompletedAt);
