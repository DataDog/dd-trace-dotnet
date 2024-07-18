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

        var ipcClientType = Samples.SampleHelpers.IpcClientType!;
        var setMessageReceivedCallbackMethod = ipcClientType.GetMethod("SetMessageReceivedCallback");
        var trySendMessageMethod = ipcClientType.GetMethod("TrySendMessage");
        using var ipcClient = (IDisposable)Activator.CreateInstance(ipcClientType, sessionId)!;

        var responseManualResetEvent = new ManualResetEventSlim();
        
        void SendMessage(object message)
        {
            trySendMessageMethod!.Invoke(ipcClient, [message]);
        }

        setMessageReceivedCallbackMethod!.Invoke(
            ipcClient,
            [
                new Action<object>(
                    message =>
                    {
                        Console.WriteLine(@"IpcClient.Message Received: " + message);
                        SendMessage("ACK: " + message);
                        responseManualResetEvent.Set();
                    })
            ]);

        SendMessage("Hello from CI Visibility Ipc Sample!");
        if (!responseManualResetEvent.Wait(30_000))
        {
            throw new Exception("Timeout waiting for response");
        }
    }
}
