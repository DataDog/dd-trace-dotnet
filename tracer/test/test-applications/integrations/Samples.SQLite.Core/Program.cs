using System;
using System.Data.SQLite;
using System.Threading;
using System.Threading.Tasks;
using Samples.DatabaseHelper;

namespace Samples.SQLite.Core
{
    internal static class Program
    {
        private static async Task Main()
        {
            var commandFactory = new DbCommandFactory($@"`SQLite-Test-{Guid.NewGuid():N}`");
            var commandExecutor = new SqliteCommandExecutor();
            var cts = new CancellationTokenSource();

            using (var connection = OpenConnection())
            {
                await RelationalDatabaseTestHarness.RunAllAsync<SQLiteCommand>(connection, commandFactory, commandExecutor, cts.Token);
            }

            // allow time to flush
            await Task.Delay(2000, cts.Token);
        }

        private static SQLiteConnection OpenConnection()
        {
            var connection = new SQLiteConnection("Data Source=:memory:");
            connection.Open();
            return connection;
        }
    }
}
