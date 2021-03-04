using System;
using Datadog.Trace.TestHelpers.NamedPipes.Server;

namespace Datadog.Trace.TestHelpers.NamedPipes.Interfaces
{
    public class MessageReceivedEventArgs : EventArgs
    {
        public MockHttpMessage Message { get; set; }
    }
}
