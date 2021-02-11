using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Samples.DatabaseHelper;
using Samples.SqlServer.Vb;

namespace Samples.SqlServer
{
    internal static class Program
    {
        private static async Task Main()
        {
            var commandFactory = new DbCommandFactory($"[System-Data-SqlClient-Test-{Guid.NewGuid():N}]");
            var commandExecutor = new SqlCommandExecutor();
            var commandExecutorVb = new SqlCommandExecutorVb();
            var cts = new CancellationTokenSource();

            // Use the connection type that is loaded by the runtime through the typical loading algorithm
            using (var connection = OpenConnection(typeof(SqlConnection)))
            {
                await RelationalDatabaseTestHarness.RunAllAsync<SqlCommand>(connection, commandFactory, commandExecutor, cts.Token);
                await RelationalDatabaseTestHarness.RunSingleAsync(connection, commandFactory, commandExecutorVb, cts.Token);
            }

            // Test the result when the ADO.NET provider assembly is loaded through Assembly.LoadFile
            // On .NET Core this results in a new assembly being loaded whose types are not considered the same
            // as the types loaded through the default loading mechanism, potentially causing type casting issues in CallSite instrumentation
            var loadFileType = AssemblyHelpers.LoadFileAndRetrieveType(typeof(SqlConnection));
            using (var connection = OpenConnection(loadFileType))
            {
                // Do not use the strongly typed SqlCommandExecutor because the type casts will fail
                await RelationalDatabaseTestHarness.RunBaseClassesAsync(connection, commandFactory, cts.Token);
            }

            // allow time to flush
            await Task.Delay(2000, cts.Token);
        }

        private static DbConnection OpenConnection(Type connectionType)
        {
            int numAttempts = 3;
            var connectionString = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING") ??
@"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Connection Timeout=60";

            for (int i = 0; i < numAttempts; i++)
            {
                DbConnection connection = null;

                try
                {
                    connection = Activator.CreateInstance(connectionType, connectionString) as DbConnection;
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
