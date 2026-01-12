namespace Samples.MassTransit.Messages;

// Request/Response pattern messages
public record CheckInventory(string ProductName, int Quantity);

public record InventoryResult(bool Available, int AvailableQuantity);

public record GetOrderStatus(Guid OrderId);

public record OrderStatusResult(Guid OrderId, string Status, DateTime LastUpdated);
