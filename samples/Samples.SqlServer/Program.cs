using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Samples.DatabaseHelper;

namespace Samples.SqlServer
{
    internal static class Program
    {
        private static async Task Main()
        {
            var commandFactory = new DbCommandFactory();
            var commandExecutor = new SqlCommandExecutor();
            var cts = new CancellationTokenSource();

            using (var connection = CreateConnection())
            {
                await RelationalDatabaseTestHarness.RunAllAsync(connection, commandFactory, commandExecutor, cts.Token);
            }

            // allow time to flush
            await Task.Delay(2000, cts.Token);
        }

        private static SqlConnection CreateConnection()
        {
            var connectionString = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING") ??
                                   @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Connection Timeout=30";

            return new SqlConnection(connectionString);
        }
    }
}
