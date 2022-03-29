using System;
using NServiceBus;

namespace ServiceBus.Minimal.NServiceBus.Shared
{
    public class OrderCompleted : IEvent
    {
        public Guid OrderId { get; set; }
    }
}
