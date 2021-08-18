using System;

namespace MassTransit.Autofac.Saga.Contracts
{
    public interface SubmitOrder
    {
        Guid OrderId { get; }
        DateTimeOffset OrderDateTime { get; }
    }
}