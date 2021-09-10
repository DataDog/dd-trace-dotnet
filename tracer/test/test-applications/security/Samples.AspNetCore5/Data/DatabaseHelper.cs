using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace Samples.AspNetCore5.Data
{
    public static class DatabaseHelper
    {
        public const string dbName = "Samples.AspNetCore5";
        public static SqlConnection GetConnectionForDb(string connectionString) => new(connectionString + $";Database='{dbName}'");
        public static void CreateAndFeedDatabase(string connectionString)
        {
            string cmdText = $"select * from master.dbo.sysdatabases where name='{dbName}'";
            using var sqlConnection = new SqlConnection(connectionString);
            sqlConnection.Open();
            using var sqlCmd = new SqlCommand(cmdText, sqlConnection);
            var reader = sqlCmd.ExecuteReader();
            reader.Read();
            var dbExists = reader.HasRows;
            sqlConnection.Close();
            if (!dbExists)
            {
                string script = File.ReadAllText(@"Data\\createdb.sql");
                {
                    using var connection = new SqlConnection(connectionString);
                    var myCommand = new SqlCommand(script, connection);
                    connection.Open();
                    myCommand.ExecuteNonQuery();
                    connection.Close();
                }
                script = File.ReadAllText(@"Data\\database.sql");
                {
                    using var connection = new SqlConnection(connectionString);
                    var myCommand = new SqlCommand(script, connection);
                    connection.Open();
                    myCommand.ExecuteNonQuery();
                    connection.Close();
                }
            }
        }
    }
}
