namespace ServiceBus.Minimal.MassTransit.Components.Activities.SuspendOrder
{
    using System;


    public interface SuspendOrderArguments
    {
        Guid OrderId { get; }
    }
}