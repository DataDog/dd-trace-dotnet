using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Samples.DatabaseHelper;

namespace Samples.Microsoft.Data.SqlClient
{
    internal static class Program
    {
        private static async Task Main()
        {
            var commandFactory = new DbCommandFactory($"[Microsoft-Data-SqlClient-Test-{Guid.NewGuid():N}]");
            var commandExecutor = new MicrosoftSqlCommandExecutor();
            var cts = new CancellationTokenSource();

            using (var connection = OpenConnection())
            {
                await RelationalDatabaseTestHarness.RunAllAsync<SqlCommand>(connection, commandFactory, commandExecutor, cts.Token);
            }

            // allow time to flush
            await Task.Delay(2000, cts.Token);
        }

        private static SqlConnection OpenConnection()
        {
            int numAttempts = 3;
            var connectionString = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING") ??
@"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Connection Timeout=60";

            for (int i = 0; i < numAttempts; i++)
            {
                SqlConnection connection = null;

                try
                {
                    connection = new SqlConnection(connectionString);
                    connection.Open();
                    return connection;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    connection?.Dispose();
                }
            }

            throw new Exception($"Unable to open connection to connection string {connectionString} after {numAttempts} attempts");
        }
    }
}
