using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Samples.DatabaseHelper;
using Samples.DatabaseHelper.NetFramework20;

namespace Samples.SqlServer.NetFramework20
{
    internal static class Program
    {
        private static async Task Main()
        {
            var commandFactory = new DbCommandFactory();
            var cts = new CancellationTokenSource();

            var sqlCommandExecutor = new SqlCommandExecutor20Adapter(new SqlCommandExecutor20());
            var dbCommandClassExecutor = new DbCommandClassExecutor20Adapter(new DbCommandClassExecutor20());
            var dbCommandInterfaceExecutor = new DbCommandInterfaceExecutor20Adapter(new DbCommandInterfaceExecutor20());
            var dbCommandInterfaceGenericExecutor = new DbCommandInterfaceGenericExecutor20Adapter<SqlCommand>(new DbCommandInterfaceGenericExecutor20<SqlCommand>());

            using (var connection = OpenConnection())
            {
                await RelationalDatabaseTestHarness.RunAllAsync(
                    connection,
                    commandFactory,
                    cts.Token,
                    sqlCommandExecutor,
                    dbCommandClassExecutor,
                    dbCommandInterfaceExecutor,
                    dbCommandInterfaceGenericExecutor);
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
