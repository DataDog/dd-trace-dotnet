using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Samples.DatabaseHelper;

namespace Samples.Npgsql
{
    internal static class Program
    {
        private static async Task Main()
        {
            var commandFactory = new DbCommandFactory();
            var commandExecutor = new NpgsqlCommandExecutor();
            var cts = new CancellationTokenSource();

            using (var connection = OpenConnection())
            {
                await RelationalDatabaseTestHarness.RunAllAsync<NpgsqlCommand>(connection, commandFactory, commandExecutor, cts.Token);
            }

            // allow time to flush
            await Task.Delay(2000, cts.Token);
        }

        private static NpgsqlConnection OpenConnection()
        {
            var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");

            if (connectionString == null)
            {
                var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
                connectionString = $"Host={host};Username=postgres;Password=postgres;Database=postgres";
            }

            var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            return connection;
        }
    }
}
