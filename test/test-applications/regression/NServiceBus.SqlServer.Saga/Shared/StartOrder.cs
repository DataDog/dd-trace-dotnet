using System;
using NServiceBus;

namespace NServiceBus.SqlServer.Saga.Shared
{
    public class StartOrder : IMessage
    {
        public Guid OrderId { get; set; }
    }
}
