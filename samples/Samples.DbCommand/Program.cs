using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Samples.DatabaseHelper;

namespace Samples.DbCommand
{
    internal class Program
    {
        private static async Task Main()
        {
            var connectionString = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING") ??
                                   @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;";

            using (var connection = new SqlConnection(connectionString))
            {
                var testQueries = new RelationalDatabaseTestHarness<DbConnection, System.Data.Common.DbCommand, DbDataReader>(
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
    }
}
