namespace ServiceBus.Minimal.MassTransit.Contracts
{
    using System;


    public interface BatchJobCompleted
    {
        Guid BatchJobId { get; }
        Guid BatchId { get; }
        DateTime Timestamp { get; }
    }
}