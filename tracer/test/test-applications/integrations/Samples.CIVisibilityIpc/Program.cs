using System;
using System.Threading;

namespace Samples.CIVisibilityIpc;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine(@"Hello from CI Visibility Ipc Sample!");
        var sessionId = Environment.GetEnvironmentVariable("IPC_SESSION_ID") ?? throw new Exception("IPC_SESSION_ID is not set");

        Console.WriteLine(@"Using SessionId: " + sessionId);

        var ipcClientType = typeof(Datadog.Trace.Tracer).Assembly.GetType("Datadog.Trace.Ci.Ipc.IpcClient")!;
        var messageReceivedEvent = ipcClientType.GetEvent("MessageReceived");
        var trySendMessageMethod = ipcClientType.GetMethod("TrySendMessage");
        using var ipcClient = (IDisposable)Activator.CreateInstance(ipcClientType, sessionId);

        var responseManualResetEvent = new ManualResetEventSlim();
        
        void SendMessage(object message)
        {
            trySendMessageMethod?.Invoke(ipcClient, [message]);
        }
        
        messageReceivedEvent?.AddEventHandler(ipcClient, new EventHandler<object>((sender, message) =>
        {
            Console.WriteLine(@"IpcClient.Message Received: " + message);
            SendMessage("ACK: " + message);
            responseManualResetEvent.Set();
        }));

        SendMessage("Hello from CI Visibility Ipc Sample!");
        if (!responseManualResetEvent.Wait(30_000))
        {
            throw new Exception("Timeout waiting for response");
        }
    }
}
