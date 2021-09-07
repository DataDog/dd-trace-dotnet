namespace ServiceBus.Minimal.MassTransit.Contracts
{
    using System;


    public interface StartBatch
    {
        Guid BatchId { get; }
    }
}