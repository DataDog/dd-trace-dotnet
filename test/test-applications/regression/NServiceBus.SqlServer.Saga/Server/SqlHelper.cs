using System.Data.SqlClient;

namespace NServiceBus.SqlServer.Saga.Server
{
    public static class SqlHelper
    {
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
