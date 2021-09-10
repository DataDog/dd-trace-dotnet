namespace ServiceBus.Minimal.MassTransit.Contracts
{
    using System;


    public interface CancelBatch
    {
        Guid BatchId { get; }

        DateTime Timestamp { get; }
    }
}