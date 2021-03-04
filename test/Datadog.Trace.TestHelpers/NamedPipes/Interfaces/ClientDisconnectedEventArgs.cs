using System;

namespace Datadog.Trace.TestHelpers.NamedPipes.Interfaces
{
    public class ClientDisconnectedEventArgs : EventArgs
    {
        public string ClientId { get; set; }
    }
}
