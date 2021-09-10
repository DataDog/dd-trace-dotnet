namespace ServiceBus.Minimal.MassTransit.Contracts
{
    using System;


    public interface BatchJobDone
    {
        Guid BatchJobId { get; }
        Guid BatchId { get; }
        DateTime Timestamp { get; }
    }
}