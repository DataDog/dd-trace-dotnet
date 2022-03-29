using global::MassTransit;
using System;

namespace ServiceBus.Minimal.MassTransit.Contracts
{
    public interface BatchJobFailed
    {
        Guid BatchJobId { get; }
        Guid BatchId { get; }
        Guid OrderId { get; }
        DateTime Timestamp { get; }
        ExceptionInfo ExceptionInfo { get; set; }
    }
}
