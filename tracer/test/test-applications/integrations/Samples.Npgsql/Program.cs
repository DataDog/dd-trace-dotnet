using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Samples.DatabaseHelper;

namespace Samples.Npgsql
{
    internal static class Program
    {
        private static async Task Main()
        {
            var commandFactory = new DbCommandFactory($"\"Npgsql-Test-{Guid.NewGuid():N}\"");
            var commandExecutor = new NpgsqlCommandExecutor();
            var cts = new CancellationTokenSource();

            // Use the connection type that is loaded by the runtime through the typical loading algorithm
            using (var connection = OpenConnection(typeof(NpgsqlConnection)))
            {
                await RelationalDatabaseTestHarness.RunAllAsync<NpgsqlCommand>(connection, commandFactory, commandExecutor, cts.Token);

#if HAS_BATCH_SUPPORT
                using (var root = SampleHelpers.CreateScope("RunBatchAsync"))
                {
                    // now an extra test running a batch command
                    // cannot be added to the test harness because ado.net doesn't provide a generic way to build a batch query
                    // like there is for connection.CreateCommand(), instead we have to build it using framework's specific methods.
                    Console.WriteLine("BATCH command");
                    var batch = new NpgsqlBatch((NpgsqlConnection)connection);
                    batch.BatchCommands.Add(new NpgsqlBatchCommand($"DROP TABLE IF EXISTS my_table"));
                    batch.BatchCommands.Add(new NpgsqlBatchCommand($"CREATE TABLE my_table (Id int PRIMARY KEY, Name varchar(100))"));
                    var insertcommand = new NpgsqlBatchCommand($"INSERT INTO my_table (Id, Name) VALUES (@Id, @Name)");
                    insertcommand.Parameters.Add(new NpgsqlParameter("Id", value: 1));
                    insertcommand.Parameters.Add(new NpgsqlParameter("Name", "Name1"));
                    batch.BatchCommands.Add(insertcommand);
                    await batch.ExecuteNonQueryAsync(cts.Token);
                }
#endif
            }

            // Test the result when the ADO.NET provider assembly is loaded through Assembly.LoadFile
            // On .NET Core this results in a new assembly being loaded whose types are not considered the same
            // as the types loaded through the default loading mechanism, potentially causing type casting issues in CallSite instrumentation
            var loadFileType = AssemblyHelpers.LoadFileAndRetrieveType(typeof(NpgsqlConnection));
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
            var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");

            if (connectionString == null)
            {
                var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
                connectionString = $"Host={host};Username=postgres;Password=postgres;Database=postgres";
            }

            var connection = Activator.CreateInstance(connectionType, connectionString) as DbConnection;
            connection.Open();
            return connection;
        }
    }
}
