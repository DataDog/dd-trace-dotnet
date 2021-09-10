using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Samples.DatabaseHelper;

namespace Samples.Microsoft.Data.Sqlite
{
    internal static class Program
    {
        private static async Task Main()
        {
            var commandFactory = new DbCommandFactory($@"`Sqlite-Test-{Guid.NewGuid():N}`");
            var commandExecutor = new SqliteCommandExecutor();
            var cts = new CancellationTokenSource();

            using (var connection = OpenConnection())
            {
                await RelationalDatabaseTestHarness.RunAllAsync<SqliteCommand>(connection, commandFactory, commandExecutor, cts.Token);
            }

            // allow time to flush
            await Task.Delay(2000, cts.Token);
        }

        private static SqliteConnection OpenConnection()
        {
            SQLitePCL.Batteries.Init();
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            return connection;
        }
    }
}
