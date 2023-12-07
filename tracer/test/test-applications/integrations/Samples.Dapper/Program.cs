using System;
using System.Threading.Tasks;
using Npgsql;
using Samples.DatabaseHelper;

namespace Samples.Dapper
{
    internal static class Program
    {
        private static async Task Main()
        {
            using (var connection = CreateConnection())
            {
                var testQueries = new DapperTestHarness();
                await testQueries.RunAsync(connection);
            }
        }

        private static NpgsqlConnection CreateConnection()
        {
            var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");

            if (connectionString == null)
            {
                var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";

                if (!int.TryParse(Environment.GetEnvironmentVariable("POSTGRES_PORT"), out var port))
                {
                    port = 5432;
                }

                connectionString = $"Host={host};Port={port};Username=postgres;Password=postgres;Database=postgres";
            }

            return new NpgsqlConnection(connectionString);
        }
    }
}
