namespace Samples.MassTransit7.Messages;

// Command messages - Tell a service to do something
public record SubmitOrder(Guid OrderId, string CustomerName, decimal TotalAmount, List<OrderItem> Items);

public record OrderItem(string ProductName, int Quantity, decimal Price);

public record ProcessPayment(Guid OrderId, decimal Amount);

public record ShipOrder(Guid OrderId, string ShippingAddress);

public record CancelOrder(Guid OrderId, string Reason);

public record ScheduleOrderReminder(Guid OrderId, string Message);
