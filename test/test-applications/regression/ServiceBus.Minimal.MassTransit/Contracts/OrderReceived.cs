using System;

namespace ServiceBus.Minimal.MassTransit.Contracts
{
    public interface OrderReceived
    {
        Guid OrderId { get; }
        DateTimeOffset OrderDateTime { get; }
    }
}