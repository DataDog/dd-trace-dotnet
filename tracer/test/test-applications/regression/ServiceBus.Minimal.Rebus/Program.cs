using System;
using System.Diagnostics;
using System.Threading;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Routing.TypeBased;
using ServiceBus.Minimal.Rebus.Shared;
using System.Data.SqlClient;

namespace ServiceBus.Minimal.Rebus
{
    class Program
    {
        internal static readonly int NumMessagesToSend = 5;
        internal static readonly int MessageSendDelayMs = 500;

        static void Main()
        {
            var connectionString = GetSqlServerConnectionString("RebusSamples");
            EnsureDatabaseExists(connectionString);

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
                .Transport(t => t.UseSqlServer(new SqlServerTransportOptions(connectionString), "consumer.input"))
                .Start();

            SendMessages(connectionString);
            Console.WriteLine("App completed successfully");
        }

        static void SendMessages(string connectionString)
        {
            using var adapter = new BuiltinHandlerActivator();

            adapter.Handle<Reply>(async reply =>
            {
                await Console.Out.WriteLineAsync($"Got reply '{reply.Id}' (from OS process {reply.OsProcessId})");
            });

            Configure.With(adapter)
                .Logging(l => l.ColoredConsole(minLevel: LogLevel.Warn))
                .Transport(t => t.UseSqlServer(new SqlServerTransportOptions(connectionString), "producer.input"))
                .Routing(r => r.TypeBased().MapAssemblyOf<Job>("consumer.input"))
                .Start();

            for (int i = 0; i < NumMessagesToSend; i++)
            {
                adapter.Bus.Send(new Job(i)).Wait();
                Thread.Sleep(MessageSendDelayMs);
            }
        }

        static string GetSqlServerConnectionString(string overrideInitialCatalog = null)
        {
            var connectionString = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING") ??
                @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Connection Timeout=60";

            var builder = new SqlConnectionStringBuilder(connectionString);
            if (!string.IsNullOrWhiteSpace(overrideInitialCatalog))
            {
                builder.InitialCatalog = overrideInitialCatalog;
            }

            return builder.ConnectionString;
        }

        static void EnsureDatabaseExists(string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var database = builder.InitialCatalog;

            var masterConnection = connectionString.Replace(builder.InitialCatalog, "master");

            const int maxRetries = 5;
            const int delayMs = 2000;
            Exception lastException = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using (var connection = new SqlConnection(masterConnection))
                    {
                        connection.Open();

                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = $@"
IF (DB_ID('{database}') IS NULL)
    CREATE DATABASE [{database}]
";
                            command.ExecuteNonQuery();
                        }

                        Console.WriteLine($"Database '{database}' is ensured to exist.");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Console.WriteLine($"Attempt {attempt}/{maxRetries} failed: {ex.GetType().Name} - {ex.Message}");

                    if (attempt < maxRetries)
                    {
                        Thread.Sleep(delayMs);
                    }
                }
            }

            Console.Error.WriteLine($"All attempts to connect to SQL Server failed. Throwing last exception...");
            throw new InvalidOperationException(
                $"Failed to connect to SQL Server and create database '{builder.InitialCatalog}' after {maxRetries} attempts.",
                lastException
            );
        }
    }
}
