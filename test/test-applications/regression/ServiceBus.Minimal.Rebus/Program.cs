using System;
using System.Diagnostics;
using System.Threading;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Routing.TypeBased;
using ServiceBus.Minimal.Rebus.Shared;
using Rebus.Transport.InMem;

namespace ServiceBus.Minimal.Rebus
{
    class Program
    {
        internal static readonly int NumMessagesToSend = 5;
        internal static readonly int MessageSendDelayMs = 500;

        static void Main()
        {
            var network = new InMemNetwork();
            using var adapter = new BuiltinHandlerActivator();
            
            adapter.Handle<Job>(async (bus, job) =>
            {
                var keyChar = job.Id;
                var processId = Process.GetCurrentProcess().Id;
                var reply = new Reply(keyChar, processId);

                await bus.Reply(reply);
            });

            Configure.With(adapter)
                .Logging(l => l.ColoredConsole(minLevel: LogLevel.Warn))
                .Transport(t => t.UseInMemoryTransport(network, "consumer.input"))
                .Start();

            SendMessages(network);
        }

        static void SendMessages(InMemNetwork network)
        {
            using var adapter = new BuiltinHandlerActivator();

            adapter.Handle<Reply>(async reply =>
            {
                await Console.Out.WriteLineAsync($"Got reply '{reply.Id}' (from OS process {reply.OsProcessId})");
            });

            Configure.With(adapter)
                .Logging(l => l.ColoredConsole(minLevel: LogLevel.Warn))
                .Transport(t => t.UseInMemoryTransport(network, "producer.input"))
                .Routing(r => r.TypeBased().MapAssemblyOf<Job>("consumer.input"))
                .Start();

            for (int i = 0; i < NumMessagesToSend; i++)
            {
                adapter.Bus.Send(new Job(i)).Wait();
                Thread.Sleep(MessageSendDelayMs);
            }
        }
    }
}
