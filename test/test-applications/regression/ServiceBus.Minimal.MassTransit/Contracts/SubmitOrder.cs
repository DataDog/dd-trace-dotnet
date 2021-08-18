using System;

namespace ServiceBus.Minimal.MassTransit.Contracts
{
    public interface SubmitOrder
    {
        Guid OrderId { get; }
        DateTimeOffset OrderDateTime { get; }
    }
}