using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Samples.OpenTelemetry.Sqlite
{
    /// <summary>
    /// Exercises the cases that shape a database client span under the OpenTelemetry database
    /// semantic conventions, using an in-memory SQLite database so no external server is needed.
    /// The set is deliberately small so the OTLP snapshot stays readable.
    /// </summary>
    internal static class Program
    {
        private const string DatabaseName = "Sqlite-Test";

        private static async Task Main()
        {
            SQLitePCL.Batteries.Init();

            using (var connection = new SqliteConnection($"Data Source={DatabaseName};Mode=Memory;Cache=Shared"))
            {
                connection.Open();

                // A DDL statement, whose numeric literals are replaced with placeholders
                ExecuteNonQuery(connection, "CREATE TABLE Employees (Id INTEGER PRIMARY KEY, Name VARCHAR(100), Salary INTEGER)");

                // Literal values, which must never reach "db.query.text"
                ExecuteNonQuery(connection, "INSERT INTO Employees (Id, Name, Salary) VALUES (1, 'Alice', 90000)");

                // A parameterized statement, whose placeholders are left alone
                ExecuteNonQueryWithParameter(connection, "INSERT INTO Employees (Id, Name, Salary) VALUES (@id, @name, @salary)", 2, "Bob", 80000);

                // The three execution methods take different code paths through the instrumentation
                ExecuteScalar(connection, "SELECT COUNT(*) FROM Employees WHERE Salary > 50000");
                ExecuteReader(connection, "SELECT Id, Name FROM Employees WHERE Name = 'Alice'");
                await ExecuteReaderAsync(connection, "SELECT Id, Name FROM Employees ORDER BY Id");

                // An IN clause, which the specification allows to be reported either collapsed or not
                ExecuteReader(connection, "SELECT Name FROM Employees WHERE Id IN (1, 2, 3)");

                // A failing statement, which reports "error.type" and "db.response.status_code"
                ExecuteFailingQuery(connection, "SELECT * FROM ThisTableDoesNotExist");
            }

            // allow time to flush
            await Task.Delay(2000);
        }

        private static void ExecuteNonQuery(SqliteConnection connection, string commandText)
        {
            using var command = connection.CreateCommand();
            command.CommandText = commandText;
            Console.WriteLine($"ExecuteNonQuery: {command.ExecuteNonQuery()} row(s)");
        }

        private static void ExecuteNonQueryWithParameter(SqliteConnection connection, string commandText, int id, string name, int salary)
        {
            using var command = connection.CreateCommand();
            command.CommandText = commandText;
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@salary", salary);
            Console.WriteLine($"ExecuteNonQuery: {command.ExecuteNonQuery()} row(s)");
        }

        private static void ExecuteScalar(SqliteConnection connection, string commandText)
        {
            using var command = connection.CreateCommand();
            command.CommandText = commandText;
            Console.WriteLine($"ExecuteScalar: {command.ExecuteScalar()}");
        }

        private static void ExecuteReader(SqliteConnection connection, string commandText)
        {
            using var command = connection.CreateCommand();
            command.CommandText = commandText;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                Console.WriteLine($"ExecuteReader: {reader.GetValue(0)}");
            }
        }

        private static async Task ExecuteReaderAsync(SqliteConnection connection, string commandText)
        {
            using var command = connection.CreateCommand();
            command.CommandText = commandText;

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                Console.WriteLine($"ExecuteReaderAsync: {reader.GetValue(0)}");
            }
        }

        private static void ExecuteFailingQuery(SqliteConnection connection, string commandText)
        {
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = commandText;
                using var reader = command.ExecuteReader();
            }
            catch (SqliteException ex)
            {
                Console.WriteLine($"Expected failure: {ex.SqliteErrorCode} {ex.Message}");
            }
        }
    }
}
