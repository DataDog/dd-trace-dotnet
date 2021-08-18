using System;

namespace MassTransit.Autofac.Saga.Contracts
{
    public interface OrderStateCreated
    {
        Guid OrderId { get; }
        DateTime Timestamp { get; }
    }
}