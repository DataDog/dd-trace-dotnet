namespace ServiceBus.Minimal.MassTransit.Contracts
{
    using System;


    public interface BatchStatus
    {
        Guid BatchId { get; }

        DateTime Timestamp { get; }

        int ProcessingJobCount { get; }

        int UnprocessedJobCount { get; }

        string State { get; }
    }
}