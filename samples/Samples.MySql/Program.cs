using System;
using System.Data;
using System.Data.Common;
using System.Threading;
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
            var cts = new CancellationTokenSource();
            var commandFactory = new DbCommandFactory();

            using (var root = Tracer.Instance.StartActive("root"))
            {
                // TODO: enable this after adding a MySql-specific integration
                using (var connection = CreateConnection())
                {
                    // using DbDataReader here (instead of MySqlDataReader)
                    // let's us run the ExecuteReaderAsync() overloads
                    var commandExecutor = new DbCommandExecutor<MySqlCommand, DbDataReader>(
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

                    var testQueries = new RelationalDatabaseTestHarness<MySqlConnection, MySqlCommand, DbDataReader>(commandFactory, commandExecutor);


                    await testQueries.RunAsync(connection, "MySqlCommand", cts.Token);
                }

                await Task.Delay(100);

#if !NET452
                // use DbCommandWrapper to reference DbCommand in netstandard.dll
                using (var connection = CreateConnection())
                {
                    var commandExecutor = DbCommandExecutor.GetDbWrapperExecutor();
                    var testQueries = new RelationalDatabaseTestHarness<DbConnection, DbCommand, DbDataReader>(commandFactory, commandExecutor);
                    await testQueries.RunAsync(connection, "DbCommandWrapper", cts.Token);
                }

                await Task.Delay(100);
#endif

                using (var connection = CreateConnection())
                {
                    var commandExecutor = DbCommandExecutor.GetDbCommandExecutor();
                    var testQueries = new RelationalDatabaseTestHarness<DbConnection, DbCommand, DbDataReader>(commandFactory, commandExecutor);
                    await testQueries.RunAsync(connection, "DbCommand", cts.Token);
                }

                await Task.Delay(100);

                using (var connection = CreateConnection())
                {
                    var commandExecutor = DbCommandExecutor.GetIDbCommandExecutor();
                    var testQueries = new RelationalDatabaseTestHarness<IDbConnection, IDbCommand, IDataReader>(commandFactory, commandExecutor);
                    await testQueries.RunAsync(connection, "IDbCommand", cts.Token);
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
