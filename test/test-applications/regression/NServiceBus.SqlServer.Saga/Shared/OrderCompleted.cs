using System;
using NServiceBus;

namespace NServiceBus.SqlServer.Saga.Shared
{
    public class OrderCompleted : IEvent
    {
        public Guid OrderId { get; set; }
    }
}
