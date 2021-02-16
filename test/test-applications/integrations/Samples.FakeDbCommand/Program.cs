using System;
using System.Threading;
using System.Threading.Tasks;
using Samples.DatabaseHelper;

namespace Samples.FakeDbCommand
{
    internal static class Program
    {
        private static async Task Main()
        {
            var commandFactory = new DbCommandFactory($"[FakeDbCommand-Test-{Guid.NewGuid():N}]");
            var commandExecutor = new FakeCommandExecutor();
            var cts = new CancellationTokenSource();

            using (var connection = OpenConnection())
            {
                await RelationalDatabaseTestHarness.RunAllAsync<FakeCommand>(connection, commandFactory, commandExecutor, cts.Token);
            }

            // allow time to flush
            await Task.Delay(2000, cts.Token);
        }

        private static FakeConnection OpenConnection()
        {
            var connection = new FakeConnection();
            connection.Open();
            return connection;
        }
    }
}
