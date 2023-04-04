
namespace MassTransitContracts
{
    using System;

    public interface ContentReceived
    {
        Guid Id { get; }
        string NodeId { get; }
    }
}
