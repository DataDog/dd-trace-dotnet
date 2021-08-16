using System;
using NServiceBus;

namespace NServiceBus.MongoDB.Saga.Shared
{
    public class OrderCompleted : IEvent
    {
        public Guid OrderId { get; set; }
    }
}
