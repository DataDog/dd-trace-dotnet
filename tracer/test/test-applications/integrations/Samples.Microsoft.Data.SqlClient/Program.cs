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
            try
            {
                return await RunAsync();
            }
            catch (Exception ex) when (IsTransportError(ex))
            {
                Console.WriteLine("Transport-level SQL error, skipping test");
                Console.WriteLine(ex.ToString());
                return 13;
            }
        }

        private static async Task<int> RunAsync()
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

            // Flush the first phase's trace before starting the next phase. Each phase produces a single
            // large trace, so draining this one first stops the two from competing for the writer's buffers
            // (a locally dropped trace here would fail the exact span-count assertion in the test).
            await SampleHelpers.ForceTracerFlushAsync();

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

            // Flush before exit so all spans are delivered (deterministic, unlike a fixed delay).
            await SampleHelpers.ForceTracerFlushAsync();
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
            => IsTransportError(ex) ||
               ex.Number == -2 ||  // Connection timeout
               ex.Number == 258;   // Connection timeout

        static bool IsTransportError(Exception ex)
        {
            // Assembly.LoadFile creates a distinct SqlException type identity, so use the full name
            // and reflection instead of casting to the compile-time Microsoft.Data.SqlClient type.
            if (ex.GetType().FullName != typeof(SqlException).FullName ||
                ex.GetType().GetProperty(nameof(SqlException.Number))?.GetValue(ex) is not int errorNumber)
            {
                return false;
            }

            return errorNumber == -1 ||      // Generic network error
                   errorNumber == 0 ||       // Driver-level transport error
                   errorNumber == 53 ||      // SQL Server not found
                   errorNumber == 10053 ||   // Connection aborted
                   errorNumber == 10054 ||   // Connection reset
                   errorNumber == 10060 ||   // Connection timeout
                   errorNumber == 11001;     // DNS failure
        }
    }
}
