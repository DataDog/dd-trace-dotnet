using System;

namespace Datadog.Trace.TestHelpers.NamedPipes.Interfaces
{
    public class ClientConnectedEventArgs : EventArgs
    {
        public string ClientId { get; set; }
    }
}
