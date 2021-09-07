namespace ServiceBus.Minimal.MassTransit.Contracts
{
    using System;
    using Enums;


    public interface ProcessBatchJob
    {
        Guid BatchJobId { get; }
        Guid BatchId { get; }
        DateTime Timestamp { get; }
        Guid OrderId { get; }
        BatchAction Action { get; }
    }
}