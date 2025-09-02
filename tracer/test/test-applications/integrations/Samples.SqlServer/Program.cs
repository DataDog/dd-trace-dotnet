using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Samples.DatabaseHelper;
using Samples.SqlServer.Vb;

namespace Samples.SqlServer
{
    internal static class Program
    {
        private static async Task<int> Main()
        {
            try
            {
                return await AsyncMain();
            }
            catch (SqlException ex) when (IsConnectionError(ex) || IsSocketError(ex))
            {
                Console.WriteLine("Transport-level SQL error, skipping test");
                Console.WriteLine(ex.ToString());
                return 13; // Skip code
            }

            // Some well-known values, and some values from
            // https://learn.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlerror.number?view=net-9.0-pp
            static bool IsConnectionError(SqlException ex)
                => ex.Number == 53 || // SQL Server not found
                   ex.Number == 10054 || // Connection reset by peer
                   ex.Number == 10060 || // Timeout expired
                   ex.Number == -2 || // Connection timeout
                   ex.Number == 258; // Connection timeout

            static bool IsSocketError(Exception ex)
            {
                while(ex != null)
                {
                    if (ex is SocketException { SocketErrorCode: SocketError.ConnectionReset })
                    {
                        return true;
                    }

                    ex = ex.InnerException;
                }

				return false;
            }
        }

        private static async Task<int> AsyncMain()
        {
            var commandFactory = new DbCommandFactory($"[System-Data-SqlClient-Test-{Guid.NewGuid():N}]");
            var commandExecutor = new SqlCommandExecutor();
            var commandExecutorVb = new SqlCommandExecutorVb();
            var cts = new CancellationTokenSource();
            var connectionString = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING") ??
@"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Connection Timeout=60";

            // Use the connection type that is loaded by the runtime through the typical loading algorithm
            using (var connection = OpenConnection(typeof(SqlConnection), connectionString))
            {
                if (connection is null)
                {
                    Console.WriteLine("No connection could be established. Exiting with skip code (13)");
                    return 13;
                }

                await RelationalDatabaseTestHarness.RunAllAsync<SqlCommand>(connection, commandFactory, commandExecutor, cts.Token);
                await RelationalDatabaseTestHarness.RunSingleAsync(connection, commandFactory, commandExecutorVb, cts.Token);
            }

            // Test the result when the ADO.NET provider assembly is loaded through Assembly.LoadFile
            // On .NET Core this results in a new assembly being loaded whose types are not considered the same
            // as the types loaded through the default loading mechanism, potentially causing type casting issues in CallSite instrumentation
            var loadFileType = AssemblyHelpers.LoadFileAndRetrieveType(typeof(SqlConnection));
            using (var connection = OpenConnection(loadFileType, connectionString))
            {
                if (connection is null)
                {
                    Console.WriteLine("No connection could be established. Exiting with skip code (13)");
                    return 13;
                }

                // Do not use the strongly typed SqlCommandExecutor because the type casts will fail
                await RelationalDatabaseTestHarness.RunBaseClassesAsync(connection, commandFactory, cts.Token);
            }

            // this uses a SqlConnection directly as it is easier / quicker
            // we don't have tests for stored procedures
            await StoredProcedure.RunStoredProcedureTestAsync(connectionString, cts.Token);

            // allow time to flush
            await Task.Delay(2000, cts.Token);

            return 0;
        }

        private static DbConnection OpenConnection(Type connectionType, string connectionString)
        {
            int numAttempts = 3;

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

            Console.WriteLine($"Unable to open connection to connection string {connectionString} after {numAttempts} attempts");
            return null;
        }
    }
}
