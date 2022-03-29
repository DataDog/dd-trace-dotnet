using System;
using NServiceBus;

namespace ServiceBus.Minimal.NServiceBus.Shared
{
    public class StartOrder : IMessage
    {
        public Guid OrderId { get; set; }
    }
}
