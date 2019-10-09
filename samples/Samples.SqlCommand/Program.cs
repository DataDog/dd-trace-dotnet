using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Samples.DatabaseHelper;

namespace Samples.SqlCommand
{
    internal class Program
    {
        private static async Task Main()
        {
            var connectionString = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING") ??
                                   @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;";

            using (var connection = new SqlConnection(connectionString))
            {
                var testQueries = new DatabaseTestHarness<SqlConnection, System.Data.SqlClient.SqlCommand, SqlDataReader>(
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
