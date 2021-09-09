namespace ServiceBus.Minimal.MassTransit.Contracts
{
    using System;


    public interface BatchNotFound
    {
        Guid BatchId { get; }

        DateTime Timestamp { get; }
    }
}