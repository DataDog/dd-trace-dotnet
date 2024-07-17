using System;
using System.Text;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Samples.DatabaseHelper;

namespace Samples.MySql
{
    internal static class Program
    {
        private static async Task Main()
        {
            var commandFactory = new DbCommandFactory($@"`MySql-Test-{Guid.NewGuid():N}`");
            var commandExecutor = new MySqlCommandExecutor();
            var cts = new CancellationTokenSource();

            // Use the connection type that is loaded by the runtime through the typical loading algorithm
            using (var connection = OpenConnection(typeof(MySqlConnection)))
            {
                await RelationalDatabaseTestHarness.RunAllAsync<MySqlCommand>(connection, commandFactory, commandExecutor, cts.Token);
            }

            // Test the result when the ADO.NET provider assembly is loaded through Assembly.LoadFile
            // On .NET Core this results in a new assembly being loaded whose types are not considered the same
            // as the types loaded through the default loading mechanism, potentially causing type casting issues in CallSite instrumentation
            var loadFileType = AssemblyHelpers.LoadFileAndRetrieveType(typeof(MySqlConnection));
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
            var connectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING");

            if (connectionString == null)
            {
                var oldMySqlServer = typeof(MySqlConnection).Assembly.GetName().Version.Major < 8;

                if (oldMySqlServer)
                {
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                    var host = Environment.GetEnvironmentVariable("MYSQL57_HOST") ?? "localhost";
                    var port = Environment.GetEnvironmentVariable("MYSQL57_PORT") ?? "3407";
                    connectionString = $"server={host};user=mysqldb;password=mysqldb;port={port};database=world;SslMode=None";
                }
                else
                {
                    var host = Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "localhost";
                    var port = Environment.GetEnvironmentVariable("MYSQL_PORT") ?? "3307";
                    connectionString = $"server={host};user=mysqldb;password=mysqldb;port={port};database=world";
                }
            }

            var connection = Activator.CreateInstance(connectionType, connectionString) as DbConnection;
            connection.Open();
            return connection;
        }
    }
}
