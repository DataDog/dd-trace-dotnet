namespace ServiceBus.Minimal.MassTransit.Contracts
{
    using System;
    using Enums;


    public interface BatchReceived
    {
        Guid BatchId { get; }
        DateTime Timestamp { get; }
        BatchAction Action { get; }
        Guid[] OrderIds { get; }
        int ActiveThreshold { get; }

        int? DelayInSeconds { get; }
    }
}