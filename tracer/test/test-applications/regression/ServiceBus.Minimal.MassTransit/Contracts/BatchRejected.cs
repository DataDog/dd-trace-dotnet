namespace ServiceBus.Minimal.MassTransit.Contracts
{
    using System;


    public interface BatchRejected
    {
        Guid BatchId { get; }
        DateTime Timestamp { get; }
        string Reason { get; }
    }
}