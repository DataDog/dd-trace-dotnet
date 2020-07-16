using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace;
using Samples.DatabaseHelper;

namespace Samples.SqlServer
{
    internal static class Program
    {
        private static async Task Main()
        {
            var cts = new CancellationTokenSource();
            var commandFactory = new DbCommandFactory();

            using (var root = Tracer.Instance.StartActive("root"))
            {
                using (var connection = CreateConnection())
                {
                    var commandExecutor = new DbCommandExecutor<SqlCommand, SqlDataReader>(
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

                    var testQueries = new RelationalDatabaseTestHarness<SqlConnection, SqlCommand, SqlDataReader>(commandFactory, commandExecutor);
                    await testQueries.RunAsync(connection, "SqlCommand", cts.Token);
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

        private static SqlConnection CreateConnection()
        {
            var connectionString = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING") ??
                                   @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Connection Timeout=30";

            return new SqlConnection(connectionString);
        }
    }
}
