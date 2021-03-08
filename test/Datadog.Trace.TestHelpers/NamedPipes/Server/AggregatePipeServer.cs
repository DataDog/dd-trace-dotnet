using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.TestHelpers.NamedPipes.Interfaces;
using Datadog.Trace.TestHelpers.NamedPipes.Utilities;

namespace Datadog.Trace.TestHelpers.NamedPipes.Server
{
    /// <summary>
    /// Aggregate pipe server for serving multiple requests in the test framework.
    /// </summary>
    public class AggregatePipeServer : IDisposable
    {
        private const int MaxNumberOfServerInstances = 10;

        private readonly string _pipeName;
        private readonly SynchronizationContext _synchronizationContext;
        private readonly IDictionary<string, InternalPipeServer> _servers;
        private int _currentServers;

        public AggregatePipeServer(string pipeName)
        {
            _pipeName = pipeName;
            _synchronizationContext = AsyncOperationManager.SynchronizationContext;
            _servers = new ConcurrentDictionary<string, InternalPipeServer>();
        }

        public event EventHandler<MessageReceivedEventArgs> MessageReceivedEvent;

        public event EventHandler<ClientConnectedEventArgs> ClientConnectedEvent;

        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnectedEvent;

        public static ConcurrentStack<string> Timeline { get; } = new ConcurrentStack<string>();

        public string ServerId
        {
            get { return _pipeName; }
        }

        public void Listen()
        {
            StartNamedPipeServer();
        }

        public void Stop()
        {
            foreach (var server in _servers.Values)
            {
                try
                {
                    UnregisterFromServerEvents(server);
                    server.Stop();
                }
                catch (Exception)
                {
                    Logger.Error("Failed to stop server");
                }
            }

            _servers.Clear();
        }

        public void Dispose()
        {
            try
            {
                Stop();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error on dispose: {ex}");
            }
        }

        private void StartNamedPipeServer()
        {
            while (_currentServers >= 10)
            {
                Thread.Sleep(10);
            }

            Interlocked.Increment(ref _currentServers);

            var internalServer = new InternalPipeServer(_pipeName, MaxNumberOfServerInstances);
            _servers[internalServer.Id] = internalServer;

            internalServer.ClientConnectedEvent += ClientConnectedHandler;
            internalServer.ClientDisconnectedEvent += ClientDisconnectedHandler;
            internalServer.MessageReceivedEvent += MessageReceivedHandler;

            internalServer.Listen();

            // _availableServers.Enqueue(internalServer);
        }

        private void StopNamedPipeServer(string id)
        {
            Interlocked.Decrement(ref _currentServers);
            UnregisterFromServerEvents(_servers[id]);
            _servers[id].Stop();
            _servers.Remove(id);
        }

        private void UnregisterFromServerEvents(InternalPipeServer server)
        {
            server.ClientConnectedEvent -= ClientConnectedHandler;
            server.ClientDisconnectedEvent -= ClientDisconnectedHandler;
            server.MessageReceivedEvent -= MessageReceivedHandler;
        }

        private void OnMessageReceived(MessageReceivedEventArgs eventArgs)
        {
            Timeline.Push($"Message received with {eventArgs.Message.BodyBytes.Length} bytes");
            _synchronizationContext.Post(
                e => MessageReceivedEvent.SafeInvoke(this, (MessageReceivedEventArgs)e),
                eventArgs);
        }

        private void OnClientConnected(ClientConnectedEventArgs eventArgs)
        {
            Timeline.Push($"Client connected: {eventArgs.ClientId}");
            _synchronizationContext.Post(
                e => ClientConnectedEvent.SafeInvoke(this, (ClientConnectedEventArgs)e),
                eventArgs);
        }

        private void OnClientDisconnected(ClientDisconnectedEventArgs eventArgs)
        {
            Timeline.Push($"Client disconnected: {eventArgs.ClientId}");
            _synchronizationContext.Post(
                e => ClientDisconnectedEvent.SafeInvoke(this, (ClientDisconnectedEventArgs)e),
                eventArgs);
        }

        private void ClientConnectedHandler(object sender, ClientConnectedEventArgs eventArgs)
        {
            OnClientConnected(eventArgs);

            StartNamedPipeServer();
        }

        private void ClientDisconnectedHandler(object sender, ClientDisconnectedEventArgs eventArgs)
        {
            OnClientDisconnected(eventArgs);

            StopNamedPipeServer(eventArgs.ClientId);
        }

        private void MessageReceivedHandler(object sender, MessageReceivedEventArgs eventArgs)
        {
            OnMessageReceived(eventArgs);
        }
    }
}
