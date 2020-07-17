using System;
using System.Data.Common;
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
            var cts = new CancellationTokenSource();
            var commandFactory = new DbCommandFactory();

            // using DbDataReader here (instead of NpgsqlDataReader)
            // let's us run the ExecuteReaderAsync() overloads
            var commandExecutor = new DbCommandExecutor<NpgsqlCommand, DbDataReader>(
                command => command.ExecuteNonQuery(),
                command => command.ExecuteScalar(),
                command => command.ExecuteReader(),
                (command, behavior) => command.ExecuteReader(behavior),
                command => command.ExecuteNonQueryAsync(),
                (command, ct) => command.ExecuteNonQueryAsync(ct),
                command => command.ExecuteScalarAsync(),
                (command, ct) => command.ExecuteScalarAsync(ct),
                command => command.ExecuteReaderAsync(),
                (command, behavior) => command.ExecuteReaderAsync(behavior),
                (command, ct) => command.ExecuteReaderAsync(ct),
                (command, behavior, ct) => command.ExecuteReaderAsync(behavior, ct));

            using (var connection = CreateConnection())
            {
                await RelationalDatabaseTestHarness.RunAllAsync(connection, commandFactory, commandExecutor, cts.Token);
            }

            // allow time to flush
            await Task.Delay(2000);
        }

        private static NpgsqlConnection CreateConnection()
        {
            var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");

            if (connectionString == null)
            {
                var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
                connectionString = $"Host={host};Username=postgres;Password=postgres;Database=postgres";
            }

            return new NpgsqlConnection(connectionString);
        }
    }
}
