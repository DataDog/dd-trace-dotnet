using System;
using System.Data.SqlClient;

namespace NServiceBus.SqlServer.Saga.Server
{
    public static class SqlHelper
    {
        public static string GetSqlServerConnectionString(string overrideInitialCatalog = null)
        {
            var connectionString = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING") ??
@"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Connection Timeout=60";

            var builder = new SqlConnectionStringBuilder(connectionString);
            if (!string.IsNullOrWhiteSpace(overrideInitialCatalog))
            {
                builder.InitialCatalog = overrideInitialCatalog;
            }

            return builder.ConnectionString;
        }

        public static void EnsureDatabaseExists(string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var database = builder.InitialCatalog;

            var masterConnection = connectionString.Replace(builder.InitialCatalog, "master");

            using (var connection = new SqlConnection(masterConnection))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $@"
    if(db_id('{database}') is null)
        create database [{database}]
    ";
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
