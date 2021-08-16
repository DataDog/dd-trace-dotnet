using System;
using NServiceBus;

namespace NServiceBus.MongoDB.Saga.Shared
{
    public class StartOrder : IMessage
    {
        public Guid OrderId { get; set; }
    }
}
