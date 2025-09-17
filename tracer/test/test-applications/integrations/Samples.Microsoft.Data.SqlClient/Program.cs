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
                if (connection is null)
                {
                    Console.WriteLine("No connection could be established. Exiting with skip code (13)");
                    return 13;
                }
                
                await RelationalDatabaseTestHarness.RunAllAsync<SqlCommand>(connection, commandFactory, commandExecutor, cts.Token);

#if HAS_BATCH_SUPPORT && NET6_0_OR_GREATER
                var batchCommandHandler = new SqlClientBatchCommandHandler();
                await RelationalDatabaseTestHarness.RunBatchAsync(
                    connection,
                    commandFactory,
                    batchCommandHandler,
                    cts.Token);
#endif
            }

            // Version 4.0.0 causes a hard crash
#if !SQLCLIENT_4
            // Test the result when the ADO.NET provider assembly is loaded through Assembly.LoadFile
            // On .NET Core this results in a new assembly being loaded whose types are not considered the same
            // as the types loaded through the default loading mechanism, potentially causing type casting issues in CallSite instrumentation
            var loadFileType = AssemblyHelpers.LoadFileAndRetrieveType(typeof(SqlConnection));
            using (var connection = OpenConnection(loadFileType))
            {
                // Do not use the strongly typed SqlCommandExecutor because the type casts will fail
                await RelationalDatabaseTestHarness.RunBaseClassesAsync(connection, commandFactory, cts.Token);
            }
#endif
            // allow time to flush
            await Task.Delay(2000, cts.Token);
            return 0;
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

            return null;
        }
    }
}
