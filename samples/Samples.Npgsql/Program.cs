using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace;
using Npgsql;
using Samples.DatabaseHelper;

namespace Samples.Npgsql
{
    internal static class Program
    {
        private static async Task Main()
        {
            var cts = new CancellationTokenSource();

            using (var root = Tracer.Instance.StartActive("root"))
            {
                using (var connection = CreateConnection())
                {
                    // using DbDataReader here (instead of NpgsqlDataReader)
                    // let's us run the ExecuteReaderAsync() overloads
                    var testQueries = new RelationalDatabaseTestHarness<NpgsqlConnection, NpgsqlCommand, DbDataReader>(
                        connection,
                        command => command.ExecuteNonQuery(),
                        command => command.ExecuteScalar(),
                        command => command.ExecuteReader(),
                        (command, behavior) => command.ExecuteReader(behavior),
                        command => command.ExecuteNonQueryAsync(),
                        (command, ct) => command.ExecuteNonQueryAsync(ct),
                        command => command.ExecuteScalarAsync(),
                        (command, ct) => command.ExecuteScalarAsync(ct),
                        command => command.ExecuteReaderAsync(),
                        (command, behavior) => command.ExecuteReaderAsync(behavior),
                        (command, ct) => command.ExecuteReaderAsync(ct),
                        (command, behavior, ct) => command.ExecuteReaderAsync(behavior, ct)
                    );

                    await testQueries.RunAsync("NpgsqlCommand", cts.Token);
                }

                await Task.Delay(100);

#if NETCOREAPP
                // use DbCommandWrapper to reference DbCommand in netstandard.dll
                using (var connection = CreateConnection())
                {
                    var testQueries = new RelationalDatabaseTestHarness<DbConnection, DbCommand, DbDataReader>(
                        connection,
                        command => new DbCommandWrapper(command).ExecuteNonQuery(),
                        command => new DbCommandWrapper(command).ExecuteScalar(),
                        command => new DbCommandWrapper(command).ExecuteReader(),
                        (command, behavior) => new DbCommandWrapper(command).ExecuteReader(behavior),
                        command => new DbCommandWrapper(command).ExecuteNonQueryAsync(),
                        (command, ct) => new DbCommandWrapper(command).ExecuteNonQueryAsync(ct),
                        command => new DbCommandWrapper(command).ExecuteScalarAsync(),
                        (command, ct) => new DbCommandWrapper(command).ExecuteScalarAsync(ct),
                        command => new DbCommandWrapper(command).ExecuteReaderAsync(),
                        (command, behavior) => new DbCommandWrapper(command).ExecuteReaderAsync(behavior),
                        (command, ct) => new DbCommandWrapper(command).ExecuteReaderAsync(ct),
                        (command, behavior, ct) => new DbCommandWrapper(command).ExecuteReaderAsync(behavior, ct)
                    );

                    await testQueries.RunAsync("DbCommandWrapper", cts.Token);
                }

                await Task.Delay(100);
#endif

                using (var connection = CreateConnection())
                {
                    var testQueries = new RelationalDatabaseTestHarness<DbConnection, DbCommand, DbDataReader>(
                        connection,
                        command => command.ExecuteNonQuery(),
                        command => command.ExecuteScalar(),
                        command => command.ExecuteReader(),
                        (command, behavior) => command.ExecuteReader(behavior),
                        command => command.ExecuteNonQueryAsync(),
                        (command, ct) => command.ExecuteNonQueryAsync(ct),
                        command => command.ExecuteScalarAsync(),
                        (command, ct) => command.ExecuteScalarAsync(ct),
                        command => command.ExecuteReaderAsync(),
                        (command, behavior) => command.ExecuteReaderAsync(behavior),
                        (command, ct) => command.ExecuteReaderAsync(ct),
                        (command, behavior, ct) => command.ExecuteReaderAsync(behavior, ct)
                    );

                    await testQueries.RunAsync("DbCommand", cts.Token);
                }

                await Task.Delay(100);

                using (var connection = CreateConnection())
                {
                    var testQueries = new RelationalDatabaseTestHarness<IDbConnection, IDbCommand, IDataReader>(
                        connection,
                        command => command.ExecuteNonQuery(),
                        command => command.ExecuteScalar(),
                        command => command.ExecuteReader(),
                        (command, behavior) => command.ExecuteReader(behavior),
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null
                    );

                    await testQueries.RunAsync("IDbCommand", cts.Token);
                }
            }
        }

        private static NpgsqlConnection CreateConnection()
        {
            var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");

            if (connectionString == null)
            {
                var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
                connectionString = $"Host={host};Username=postgres;Password=postgres;Database=postgres";
            }

            return new NpgsqlConnection(connectionString);
        }
    }
}
