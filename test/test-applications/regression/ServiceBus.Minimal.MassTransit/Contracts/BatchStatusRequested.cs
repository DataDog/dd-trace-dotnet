namespace ServiceBus.Minimal.MassTransit.Contracts
{
    using System;


    public interface BatchStatusRequested
    {
        Guid BatchId { get; }

        DateTime Timestamp { get; }
    }
}