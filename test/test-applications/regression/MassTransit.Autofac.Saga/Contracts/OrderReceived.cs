using System;

namespace MassTransit.Autofac.Saga.Contracts
{
    public interface OrderReceived
    {
        Guid OrderId { get; }
        DateTimeOffset OrderDateTime { get; }
    }
}