using System;
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
        }

        private static MySqlConnection CreateConnection()
        {
            var connectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING");

            if (connectionString == null)
            {
                var host = Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "localhost";
                connectionString = $"server={host};user=mysqldb;password=mysqldb;port=3306;database=dotnet_test";
            }

            return new MySqlConnection(connectionString);
        }
    }
}
