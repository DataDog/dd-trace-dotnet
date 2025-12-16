using System;
using System.Data.Common;
using System.Net.Sockets;
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
            }

            // Test the result when the ADO.NET provider assembly is loaded through Assembly.LoadFile
            // On .NET Core this results in a new assembly being loaded whose types are not considered the same
            // as the types loaded through the default loading mechanism, potentially causing type casting issues in CallSite instrumentation
            var loadFileType = AssemblyHelpers.LoadFileAndRetrieveType(typeof(SqlConnection));

            using (var connection = OpenConnection(loadFileType))
            {
                if (connection is null)
                {
                    Console.WriteLine("No connection could be established. Exiting with skip code (13)");
                    return 13;
                }

                // Do not use the strongly typed SqlCommandExecutor because the type casts will fail
                await RelationalDatabaseTestHarness.RunBaseClassesAsync(connection, commandFactory, cts.Token);
            }

            // allow time to flush
            await Task.Delay(2000, cts.Token);
            return 0;
        }

        private static DbConnection OpenConnection(Type connectionType)
        {
            const int maxAttempts = 3;
            var connectionString = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING") ??
@"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Connection Timeout=60";

            SqlException lastException = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                DbConnection connection = null;
                try
                {
                    connection = Activator.CreateInstance(connectionType, connectionString) as DbConnection;
                    connection.Open();
                    return connection;
                }
                catch (SqlException ex) when (IsRetryableConnectionError(ex))
                {
                    lastException = ex;
                    connection?.Dispose();

                    if (attempt < maxAttempts)
                    {
                        Console.WriteLine($"Connection attempt {attempt}/{maxAttempts} failed. Retrying...");
                        Console.WriteLine($"SqlException Number: {ex.Number}, State: {ex.State}, Class: {ex.Class}");
                        Console.WriteLine($"Message: {ex.Message}");
                        Thread.Sleep(1000 * attempt);
                    }
                }
                catch (SqlException ex) when (!IsRetryableConnectionError(ex))
                {
                    Console.WriteLine($"Fatal SqlException Number: {ex.Number}, State: {ex.State}, Class: {ex.Class}");
                    Console.WriteLine($"Message: {ex.Message}");
                    throw;
                }
                catch (Exception ex)
                {
                    // Other errors (reflection issues, etc.) should fail the test
                    Console.WriteLine($"Unexpected error opening connection: {ex}");
                    throw;
                }
            }

            // After all retry attempts exhausted, return null to signal connection failure
            Console.WriteLine($"Unable to establish SQL connection after {maxAttempts} attempts.");
            if (lastException != null)
            {
                Console.WriteLine($"Final SqlException Number: {lastException.Number}, State: {lastException.State}, Class: {lastException.Class}");
                Console.WriteLine($"Message: {lastException.Message}");
            }
            return null;
        }

        static bool IsRetryableConnectionError(SqlException ex)
        {
            // Known retryable error codes
            if (ex.Number == -1 ||      // Generic network error
                ex.Number == -2 ||      // Connection timeout
                ex.Number == 53 ||      // SQL Server not found
                ex.Number == 258 ||     // Connection timeout
                ex.Number == 10053 ||   // Connection aborted
                ex.Number == 10054 ||   // Connection reset
                ex.Number == 10060 ||   // Connection timeout
                ex.Number == 11001)     // DNS failure
            {
                return true;
            }

            // Number=0 with SocketException indicates network issue
            if (ex.Number == 0 && ex.InnerException is SocketException)
            {
                return true;
            }

            return false;
        }
    }
}
