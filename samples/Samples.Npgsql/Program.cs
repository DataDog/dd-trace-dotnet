using System;
using System.Data;
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
            using (var connection = CreateConnection())
            {
                // using DbDataReader here let's us run the ExecuteReaderAsync() overloads
                var testQueries = new RelationalDatabaseTestHarness<NpgsqlConnection, NpgsqlCommand, DbDataReader>(
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

#if NETCOREAPP
            // use DbCommandWrapper to reference DbCommand in netstandard.dll
            using (var connection = CreateConnection())
            {
                var testQueries = new RelationalDatabaseTestHarness<DbConnection, DbCommand, DbDataReader>(
                    connection,
                    command => new DbCommandWrapper(command).ExecuteNonQuery(),
                    command => new DbCommandWrapper(command).ExecuteScalar(),
                    command => new DbCommandWrapper(command).ExecuteReader(),
                    (command, behavior) => new DbCommandWrapper(command).ExecuteReader(behavior),
                    command => new DbCommandWrapper(command).ExecuteNonQueryAsync(),
                    command => new DbCommandWrapper(command).ExecuteScalarAsync(),
                    command => new DbCommandWrapper(command).ExecuteReaderAsync(),
                    (command, behavior) => new DbCommandWrapper(command).ExecuteReaderAsync(behavior)
                );

                await testQueries.RunAsync("DbCommandWrapper");
            }
#endif

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

            using (var connection = CreateConnection())
            {
                var testQueries = new RelationalDatabaseTestHarness<IDbConnection, IDbCommand, IDataReader>(
                    connection,
                    command => command.ExecuteNonQuery(),
                    command => command.ExecuteScalar(),
                    command => command.ExecuteReader(),
                    (command, behavior) => command.ExecuteReader(behavior),
                    executeNonQueryAsync: null,
                    executeScalarAsync: null,
                    executeReaderAsync: null,
                    executeReaderWithBehaviorAsync: null
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
