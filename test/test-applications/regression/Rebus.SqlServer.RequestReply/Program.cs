using System;
using System.Diagnostics;
using System.Threading;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Routing.TypeBased;
using Rebus.SqlServer.RequestReply.Server;
using Rebus.SqlServer.RequestReply.Shared;

namespace Rebus.SqlServer.RequestReply
{
    class Program
    {
        const string ConnectionString = @"Server=(localdb)\MSSQLLocalDB;Initial Catalog=RebusSamples;Integrated Security=true;Connection Timeout=60";
        internal static readonly int NumMessagesToSend = 5;
        internal static readonly int MessageSendDelayMs = 500;

        static void Main()
        {
            SqlHelper.EnsureDatabaseExists(ConnectionString);
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
                .Transport(t => t.UseSqlServer(new SqlServerTransportOptions(ConnectionString), "consumer.input"))
                .Start();

            SendMessages();
        }

        static void SendMessages()
        {
            using var adapter = new BuiltinHandlerActivator();

            adapter.Handle<Reply>(async reply =>
            {
                await Console.Out.WriteLineAsync($"Got reply '{reply.Id}' (from OS process {reply.OsProcessId})");
            });

            Configure.With(adapter)
                .Logging(l => l.ColoredConsole(minLevel: LogLevel.Warn))
                .Transport(t => t.UseSqlServer(new SqlServerTransportOptions(ConnectionString), "producer.input"))
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
