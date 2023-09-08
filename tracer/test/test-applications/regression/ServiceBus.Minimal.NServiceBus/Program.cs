using System;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Persistence.Sql;
using ServiceBus.Minimal.NServiceBus.Shared;

namespace ServiceBus.Minimal.NServiceBus
{
    class Program
    {
        internal static readonly int NumMessagesToSend = 5;
        internal static readonly int MessageSendDelayMs = 500;
        internal static readonly int MessageCompletionDurationMs = 1000;
        internal static readonly CountdownEvent Countdown = new CountdownEvent(NumMessagesToSend);
        private static readonly string EndpointName = "ServiceBus.Minimal.NServiceBus";

        static async Task Main()
        {
            var connectionString = GetSqlServerConnectionString("NsbSamplesSqlPersistence");
            var endpointConfiguration = new EndpointConfiguration(EndpointName);

            var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();
            persistence.SqlDialect<SqlDialect.MsSqlServer>();
            persistence.ConnectionBuilder(
                connectionBuilder: () =>
                {
                    return new SqlConnection(connectionString);
                });

            EnsureDatabaseExists(connectionString);

            var storageDirectory = Path.Combine(Path.GetTempPath(), "learningtransport");

            endpointConfiguration.UseTransport<LearningTransport>()
                                 .StorageDirectory(storageDirectory);
            endpointConfiguration.EnableInstallers();
            endpointConfiguration.EnableOutbox();

            var endpointInstance = await Endpoint.Start(endpointConfiguration)
                .ConfigureAwait(false);

            await SendMessagesAsync(storageDirectory);

            await endpointInstance.Stop()
                .ConfigureAwait(false);
            Console.WriteLine("App completed successfully");
        }

        static async Task SendMessagesAsync(string storageDirectory)
        {
            var endpointConfiguration = new EndpointConfiguration(EndpointName);
            endpointConfiguration.UsePersistence<LearningPersistence>();
            endpointConfiguration.UseTransport<LearningTransport>()
                                 .StorageDirectory(storageDirectory);

            var endpointInstance = await Endpoint.Start(endpointConfiguration)
                .ConfigureAwait(false);

            Console.WriteLine($"Sending {NumMessagesToSend} messages with a {MessageSendDelayMs}ms delay.");

            for (int i = 0; i < NumMessagesToSend; i++)
            {
                var orderId = Guid.NewGuid();
                var startOrder = new StartOrder
                {
                    OrderId = orderId
                };

                await endpointInstance.Send(EndpointName, startOrder)
                    .ConfigureAwait(false);

                Console.WriteLine($"Message #{i}: StartOrder Message sent with OrderId {orderId}");

                await Task.Delay(MessageSendDelayMs);
            }

            await endpointInstance.Stop()
                .ConfigureAwait(false);

            Countdown.Wait(5000);

            // Wait one more second to ensure the state is flushed between consecutive local runs
            await Task.Delay(1000);
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

            using (var connection = new SqlConnection(masterConnection))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $@"
    if(db_id('{database}') is null)
        create database [{database}]
    ";
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
