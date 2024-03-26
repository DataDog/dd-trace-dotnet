using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Data.SqlClient;
using System.Dynamic;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Samples.Security.AspNetCore5.Data
{
    public static class DatabaseHelper
    {
        public const string dbName = "Samples.AspNetCore5";
        public static SqliteConnection GetConnectionForDb(string connectionString) => new(connectionString);
        public static void CreateAndFeedDatabase(string connectionString)
        {
            var cmdText = $"select * from master.dbo.sysdatabases where name='{dbName}'";
            using var sqlConnection = new SqlConnection(connectionString);
            sqlConnection.Open();
            using var sqlCmd = new SqlCommand(cmdText, sqlConnection);
            var reader = sqlCmd.ExecuteReader();
            reader.Read();
            var dbExists = reader.HasRows;
            sqlConnection.Close();
            if (!dbExists)
            {
                string script = File.ReadAllText(Path.Combine(@"Data", "createdb.sql"));
                {
                    using var connection = new SqlConnection(connectionString);
                    var myCommand = new SqlCommand(script, connection);
                    connection.Open();
                    myCommand.ExecuteNonQuery();
                    connection.Close();
                }
                script = File.ReadAllText(Path.Combine(@"Data", "database.sql"));
                {
                    using var connection = new SqlConnection(connectionString);
                    var myCommand = new SqlCommand(script, connection);
                    connection.Open();
                    myCommand.ExecuteNonQuery();
                    connection.Close();
                }
            }
        }
        
        
        public static IImmutableList<dynamic> SelectDynamic(IConfiguration configuration, string query)
        {
            var connString = configuration.GetDefaultConnectionString();
            using var conn = GetConnectionForDb(connString);
            conn!.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = query;
            cmd.CommandType = CommandType.Text;
            using var reader = cmd.ExecuteReader();

            var names = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
            var result = reader.Cast<IDataRecord>().Select(record =>
            {
                var expando = new ExpandoObject() as IDictionary<string, object>;
                foreach (var name in names)
                {
                    expando[name] = record[name];
                }

                return expando as dynamic;
            }).ToImmutableList();
            conn.Close();
            return result;
        }
        
        public static int ExecuteNonQuery(IConfiguration configuration, string query)
        {
            var connString = configuration.GetDefaultConnectionString();
            using var conn = GetConnectionForDb(connString);
            conn!.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = query;
            cmd.CommandType = CommandType.Text;
            var result = cmd.ExecuteNonQuery();
            conn.Close();
            return result;
        }
    }
}
