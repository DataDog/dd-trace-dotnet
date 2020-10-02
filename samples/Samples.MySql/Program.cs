using System;
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
            var commandFactory = new DbCommandFactory();
            var commandExecutor = new MySqlCommandExecutor();
            var cts = new CancellationTokenSource();


            using (var connection = OpenConnection())
            {
                await RelationalDatabaseTestHarness.RunAllAsync<MySqlCommand>(connection, commandFactory, commandExecutor, cts.Token);
            }

            // allow time to flush
            await Task.Delay(2000, cts.Token);
        }

        private static MySqlConnection OpenConnection()
        {
            var connectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING");

            if (connectionString == null)
            {
                var host = Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "localhost";
                var port = Environment.GetEnvironmentVariable("MYSQL_PORT") ?? "3307";
                connectionString = $"server={host};user=mysqldb;password=mysqldb;port={port};database=world";
            }

            var connection = new MySqlConnection(connectionString);
            connection.Open();
            return connection;
        }
    }
}
