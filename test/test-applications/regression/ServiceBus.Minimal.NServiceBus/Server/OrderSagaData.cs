using System;
using NServiceBus;

namespace ServiceBus.Minimal.NServiceBus.Server
{
    #region sagadata

    public class OrderSagaData : ContainSagaData
    {
        public Guid OrderId { get; set; }

        public string OrderDescription { get; set; }
    }

    #endregion
}
