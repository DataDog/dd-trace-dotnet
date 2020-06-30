using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Datadog.Trace;
using MySql.Data.MySqlClient;
using Samples.DatabaseHelper;

namespace Samples.MySql
{
    internal static class Program
    {
        private static async Task Main()
        {
            using (var root = Tracer.Instance.StartActive("root"))
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


                    await testQueries.RunAsync("MySqlCommand");
                }

                await Task.Delay(100);
                */

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

                await Task.Delay(100);
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

                    await testQueries.RunAsync("DbCommand");
                }

                await Task.Delay(100);

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

                    await testQueries.RunAsync("IDbCommand");
                }
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
