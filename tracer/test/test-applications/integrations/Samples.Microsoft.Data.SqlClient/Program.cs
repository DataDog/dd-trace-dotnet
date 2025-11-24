using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Samples.DatabaseHelper;

namespace Samples.Microsoft.Data.SqlClient
{
    internal static class Program
    {
        private static async Task<int> Main()
        {
            var commandFactory = new DbCommandFactory($"[Microsoft-Data-SqlClient-Test-{Guid.NewGuid():N}]");
            var commandExecutor = new MicrosoftSqlCommandExecutor();
            var cts = new CancellationTokenSource();

            using (var connection = OpenConnection(typeof(SqlConnection)))
            {
                await RelationalDatabaseTestHarness.RunAllAsync<SqlCommand>(connection, commandFactory, commandExecutor, cts.Token);
            }

            // Test the result when the ADO.NET provider assembly is loaded through Assembly.LoadFile
            // On .NET Core this results in a new assembly being loaded whose types are not considered the same
            // as the types loaded through the default loading mechanism, potentially causing type casting issues in CallSite instrumentation
            var loadFileType = AssemblyHelpers.LoadFileAndRetrieveType(typeof(SqlConnection));

            try
            {
                using (var connection = OpenConnection(loadFileType))
                {
                    // Do not use the strongly typed SqlCommandExecutor because the type casts will fail
                    await RelationalDatabaseTestHarness.RunBaseClassesAsync(connection, commandFactory, cts.Token);
                }
            }
            catch(SqlException ex)
            {
                Console.WriteLine("No SQL connection could be established. Exiting with skip code (13)");
                Console.WriteLine("Exception during execution " + ex);
                return 13;
            }

            // allow time to flush
            await Task.Delay(2000, cts.Token);
            return 0;
        }

        private static DbConnection OpenConnection(Type connectionType)
        {
            var remainingAttempts = 3;
            var connectionString = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING") ??
@"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Connection Timeout=60";

            DbConnection connection = null;
            retry:
            try
            {
                connection = Activator.CreateInstance(connectionType, connectionString) as DbConnection;
                connection.Open();
                return connection;
            }
            catch (Exception ex)
            {
                if (remainingAttempts > 0)
                {
                    Console.WriteLine(ex);
                    connection?.Dispose();
                    remainingAttempts--;
                    goto retry;
                }

                // else
                throw;
            }
        }
    }
}
