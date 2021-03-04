using System;

namespace Datadog.Trace.TestHelpers.NamedPipes.Interfaces
{
    internal interface ICommunicationServer : ICommunication
    {
        event EventHandler<MessageReceivedEventArgs> MessageReceivedEvent;

        event EventHandler<ClientConnectedEventArgs> ClientConnectedEvent;

        event EventHandler<ClientDisconnectedEventArgs> ClientDisconnectedEvent;

        string ServerId { get; }
    }
}
