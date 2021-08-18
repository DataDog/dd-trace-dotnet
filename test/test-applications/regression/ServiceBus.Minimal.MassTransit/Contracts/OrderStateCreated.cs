using System;

namespace ServiceBus.Minimal.MassTransit.Contracts
{
    public interface OrderStateCreated
    {
        Guid OrderId { get; }
        DateTime Timestamp { get; }
    }
}