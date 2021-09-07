namespace ServiceBus.Minimal.MassTransit.Contracts
{
    using System;
    using Enums;


    public interface BatchJobReceived
    {
        Guid BatchJobId { get; }
        Guid BatchId { get; }
        Guid OrderId { get; }
        DateTime Timestamp { get; }
        BatchAction Action { get; }
    }
}