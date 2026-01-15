namespace Samples.MassTransit7.Messages;

public record ProcessPayment(Guid OrderId, decimal Amount);

public record ShipOrder(Guid OrderId, string ShippingAddress);
