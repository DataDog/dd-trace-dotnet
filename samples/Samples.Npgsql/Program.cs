using System;
using System.Data.Common;
using System.Threading.Tasks;
using Npgsql;
using Samples.DatabaseHelper;

namespace Samples.Npgsql
{
    internal static class Program
    {
        private static async Task Main()
        {
            /* TODO: enable this after adding a Npgsql-specific integration
            using (var connection = CreateConnection())
            {
                var testQueries = new RelationalDatabaseTestHarness<NpgsqlConnection, NpgsqlCommand, NpgsqlDataReader>(
                    connection,
                    command => command.ExecuteNonQuery(),
                    command => command.ExecuteScalar(),
                    command => command.ExecuteReader(),
                    (command, behavior) => command.ExecuteReader(behavior),
                    command => command.ExecuteNonQueryAsync(),
                    command => command.ExecuteScalarAsync(),
                    executeReaderAsync: null,
                    executeReaderWithBehaviorAsync: null
                );


                await testQueries.RunAsync();
            }
            */

            using (var connection = CreateConnection())
            {
                var testQueries = new RelationalDatabaseTestHarness<DbConnection, DbCommand, DbDataReader>(
                    connection,
                    command => command.ExecuteNonQuery(),
                    command => command.ExecuteScalar(),
                    command => command.ExecuteReader(),
                    (command, behavior) => command.ExecuteReader(behavior),
                    command => command.ExecuteNonQueryAsync(),
                    command => command.ExecuteScalarAsync(),
                    command => command.ExecuteReaderAsync(),
                    (command, behavior) => command.ExecuteReaderAsync(behavior)
                );

                await testQueries.RunAsync();
            }
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
