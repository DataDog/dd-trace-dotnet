using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace Samples.SqlCommand
{
    internal class Program
    {
        private const string DropCommandText = "DROP TABLE IF EXISTS Employees; CREATE TABLE Employees (Id int PRIMARY KEY CLUSTERED, Name nvarchar(100));";
        private const string InsertCommandText = "INSERT INTO Employees (Id, Name) VALUES (@Id, @Name);";
        private const string SelectOneCommandText = "SELECT Name FROM Employees WHERE Id=@Id;";
        private const string UpdateCommandText = "UPDATE Employees SET Name=@Name WHERE Id=@Id;";
        private const string SelectManyCommandText = "SELECT * FROM Employees WHERE Id=@Id;";
        private const string DeleteCommandText = "DELETE FROM Employees WHERE Id=@Id;";

        private static async Task Main()
        {
            var connectionString = GetConnectionString();

            using (var connection = new SqlConnection(connectionString))
            {
                Console.WriteLine("Calling synchronous methods:");
                ExecuteQueries(connection);

                Console.WriteLine();
                Console.WriteLine("Calling asynchronous methods:");
                await ExecuteQueriesAsync(connection);
            }
        }

        private static string GetConnectionString()
        {
            return Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING") ??
                   @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;";
        }

        private static void ExecuteQueries(SqlConnection connection)
        {
            connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = DropCommandText;
                int records = command.ExecuteNonQuery();
            }

            using (var command = connection.CreateCommand())
            {
                command.Parameters.AddWithValue("Id", 1);
                command.Parameters.AddWithValue("Name", "Name1");
                command.CommandText = InsertCommandText;
                int records = command.ExecuteNonQuery();

                Console.WriteLine($"Inserted {records} record(s).");
            }

            using (var command = connection.CreateCommand())
            {
                command.Parameters.AddWithValue("Id", 1);
                command.CommandText = SelectOneCommandText;

                var name = command.ExecuteScalar() as string ?? "(null)";
                Console.WriteLine($"Selected scalar `{name}`.");
            }

            using (var command = connection.CreateCommand())
            {
                command.Parameters.AddWithValue("Name", "Name2");
                command.Parameters.AddWithValue("Id", 1);
                command.CommandText = UpdateCommandText;
                int records = command.ExecuteNonQuery();

                Console.WriteLine($"Updated {records} record(s).");
            }

            using (var command = connection.CreateCommand())
            {
                command.Parameters.AddWithValue("Id", 1);
                command.CommandText = SelectManyCommandText;

                using (var reader = command.ExecuteReader())
                {
                    var employees = reader.AsDataRecords()
                                          .Select(
                                               r => new
                                               {
                                                   Id = (int)r["Id"],
                                                   Name = (string)r["Name"]
                                               })
                                          .ToList();

                    Console.WriteLine($"Selected {employees.Count} record(s).");
                }

                using (var reader = command.ExecuteReader(CommandBehavior.Default))
                {
                    var employees = reader.AsDataRecords()
                                          .Select(
                                               r => new
                                               {
                                                   Id = (int)r["Id"],
                                                   Name = (string)r["Name"]
                                               })
                                          .ToList();

                    Console.WriteLine($"Selected {employees.Count} record(s) with `CommandBehavior.Default`.");
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.Parameters.AddWithValue("Id", 1);
                command.CommandText = DeleteCommandText;
                int records = command.ExecuteNonQuery();

                Console.WriteLine($"Deleted {records} record(s).");
            }

            connection.Close();
        }

        private static async Task ExecuteQueriesAsync(SqlConnection connection)
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = DropCommandText;
                int records = await command.ExecuteNonQueryAsync();
            }

            using (var command = connection.CreateCommand())
            {
                command.Parameters.AddWithValue("Id", 1);
                command.Parameters.AddWithValue("Name", "Name1");
                command.CommandText = InsertCommandText;
                int records = await command.ExecuteNonQueryAsync();

                Console.WriteLine($"Inserted {records} record(s).");
            }

            using (var command = connection.CreateCommand())
            {
                command.Parameters.AddWithValue("Id", 1);
                command.CommandText = SelectOneCommandText;
                object nameObj = await command.ExecuteScalarAsync();

                var name = nameObj as string ?? "(null)";
                Console.WriteLine($"Selected scalar `{name}`.");
            }

            using (var command = connection.CreateCommand())
            {
                command.Parameters.AddWithValue("Name", "Name2");
                command.Parameters.AddWithValue("Id", 1);
                command.CommandText = UpdateCommandText;
                int records = await command.ExecuteNonQueryAsync();

                Console.WriteLine($"Updated {records} record(s).");
            }

            using (var command = connection.CreateCommand())
            {
                command.Parameters.AddWithValue("Id", 1);
                command.CommandText = SelectManyCommandText;

                using (var reader = await command.ExecuteReaderAsync())
                {
                    var employees = reader.AsDataRecords()
                                          .Select(
                                               r => new
                                               {
                                                   Id = (int)r["Id"],
                                                   Name = (string)r["Name"]
                                               })
                                          .ToList();

                    Console.WriteLine($"Selected {employees.Count} record(s).");
                }

                using (var reader = await command.ExecuteReaderAsync(CommandBehavior.Default))
                {
                    var employees = reader.AsDataRecords()
                                          .Select(
                                               r => new
                                               {
                                                   Id = (int)r["Id"],
                                                   Name = (string)r["Name"]
                                               })
                                          .ToList();

                    Console.WriteLine($"Selected {employees.Count} record(s) with `CommandBehavior.Default`.");
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.Parameters.AddWithValue("Id", 1);
                command.CommandText = DeleteCommandText;
                int records = await command.ExecuteNonQueryAsync();

                Console.WriteLine($"Deleted {records} record(s).");
            }

            connection.Close();
        }
    }
}
