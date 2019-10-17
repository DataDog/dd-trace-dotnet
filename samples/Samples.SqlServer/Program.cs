using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace;
using Samples.DatabaseHelper;

namespace Samples.SqlServer
{
    internal static class Program
    {
        private static async Task Main()
        {
            using (var root = Tracer.Instance.StartActive("root"))
            {
                using (var connection = CreateConnection())
                {
                    var testQueries = new RelationalDatabaseTestHarness<SqlConnection, SqlCommand, SqlDataReader>(
                        connection,
                        command => command.ExecuteNonQuery(),
                        command => command.ExecuteScalar(),
                        command => command.ExecuteReader(),
                        (command, behavior) => command.ExecuteReader(behavior),
                        command => command.ExecuteNonQueryAsync(),
                        command => command.ExecuteScalarAsync(),
                        command => command.ExecuteReaderAsync(),
                        (command, behavior) => command.ExecuteReaderAsync(behavior)
                    );


                    await testQueries.RunAsync();
                }

                using (var connection = CreateConnection())
                {
                    var testQueries = new RelationalDatabaseTestHarness<DbConnection, DbCommand, DbDataReader>(
                        connection,
                        command => command.ExecuteNonQuery(),
                        command => command.ExecuteScalar(),
                        command => command.ExecuteReader(),
                        (command, behavior) => command.ExecuteReader(behavior),
                        command => command.ExecuteNonQueryAsync(),
                        command => command.ExecuteScalarAsync(),
                        command => command.ExecuteReaderAsync(),
                        (command, behavior) => command.ExecuteReaderAsync(behavior)
                    );

                    await testQueries.RunAsync();
                }
            }
        }

        private static SqlConnection CreateConnection()
        {
            var connectionString = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING") ??
                                   @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;";

            return new SqlConnection(connectionString);
        }
    }
}
