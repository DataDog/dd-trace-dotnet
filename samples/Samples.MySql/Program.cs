using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Samples.DatabaseHelper;

namespace Samples.MySql
{
    internal static class Program
    {
        private static async Task Main()
        {
            /* TODO: enable this after adding a MySql-specific integration
            using (var connection = CreateConnection())
            {
                var testQueries = new RelationalDatabaseTestHarness<MySqlConnection, MySqlCommand, MySqlDataReader>(
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

        private static MySqlConnection CreateConnection()
        {
            var connectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING");

            if (connectionString == null)
            {
                var host = Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "localhost";
                var port = Environment.GetEnvironmentVariable("MYSQL_PORT") ?? "3307";
                connectionString = $"server={host};user=mysqldb;password=mysqldb;port={port};database=world";
            }

            return new MySqlConnection(connectionString);
        }
    }
}
