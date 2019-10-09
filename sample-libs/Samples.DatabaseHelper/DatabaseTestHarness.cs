using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace;

namespace Samples.DatabaseHelper
{
    public class DatabaseTestHarness<TConnection, TCommand, TDataReader>
        where TConnection : DbConnection
        where TCommand : DbCommand
        where TDataReader : DbDataReader
    {
        private const string DropCommandText = "DROP TABLE IF EXISTS Employees; CREATE TABLE Employees (Id int PRIMARY KEY CLUSTERED, Name nvarchar(100));";
        private const string InsertCommandText = "INSERT INTO Employees (Id, Name) VALUES (@Id, @Name);";
        private const string SelectOneCommandText = "SELECT Name FROM Employees WHERE Id=@Id;";
        private const string UpdateCommandText = "UPDATE Employees SET Name=@Name WHERE Id=@Id;";
        private const string SelectManyCommandText = "SELECT * FROM Employees WHERE Id=@Id;";
        private const string DeleteCommandText = "DELETE FROM Employees WHERE Id=@Id;";

        private readonly TConnection _connection;

        private readonly Func<TCommand, int> _executeNonQuery;
        private readonly Func<TCommand, object> _executeScalar;
        private readonly Func<TCommand, TDataReader> _executeReader;
        private readonly Func<TCommand, CommandBehavior, TDataReader> _executeReaderWithBehavior;

        private readonly Func<TCommand, Task<int>> _executeNonQueryAsync;
        private readonly Func<TCommand, Task<object>> _executeScalarAsync;
        private readonly Func<TCommand, Task<TDataReader>> _executeReaderAsync;
        private readonly Func<TCommand, CommandBehavior, Task<TDataReader>> _executeReaderWithBehaviorAsync;

        public DatabaseTestHarness(
            TConnection connection,
            Func<TCommand, int> executeNonQuery,
            Func<TCommand, object> executeScalar,
            Func<TCommand, TDataReader> executeReader,
            Func<TCommand, CommandBehavior, TDataReader> executeReaderWithBehavior,
            Func<TCommand, Task<int>> executeNonQueryAsync,
            Func<TCommand, Task<object>> executeScalarAsync,
            Func<TCommand, Task<TDataReader>> executeReaderAsync,
            Func<TCommand, CommandBehavior, Task<TDataReader>> executeReaderWithBehaviorAsync)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));

            _executeNonQuery = executeNonQuery ?? throw new ArgumentNullException(nameof(executeNonQuery));
            _executeScalar = executeScalar ?? throw new ArgumentNullException(nameof(executeScalar));
            _executeReader = executeReader ?? throw new ArgumentNullException(nameof(executeReader));
            _executeReaderWithBehavior = executeReaderWithBehavior ?? throw new ArgumentNullException(nameof(executeReaderWithBehavior));

            _executeNonQueryAsync = executeNonQueryAsync ?? throw new ArgumentNullException(nameof(executeNonQueryAsync));
            _executeScalarAsync = executeScalarAsync ?? throw new ArgumentNullException(nameof(executeScalarAsync));
            _executeReaderAsync = executeReaderAsync ?? throw new ArgumentNullException(nameof(executeReaderAsync));
            _executeReaderWithBehaviorAsync = executeReaderWithBehaviorAsync ?? throw new ArgumentNullException(nameof(executeReaderWithBehaviorAsync));
        }

        public async Task RunAsync()
        {
            using (var scopeAll = Tracer.Instance.StartActive("run.all"))
            {
                using (var scopeSync = Tracer.Instance.StartActive("run.sync"))
                {
                    _connection.Open();
                    CreateNewTable(_connection);
                    InsertRow(_connection);
                    SelectScalar(_connection);
                    UpdateRow(_connection);
                    SelectRecords(_connection);
                    DeleteRecord(_connection);
                    _connection.Close();
                }

                // leave a small space between spans, for better visibility in the UI
                await Task.Delay(TimeSpan.FromSeconds(0.1));

                using (var scopeAsync = Tracer.Instance.StartActive("run.async"))
                {
                    await _connection.OpenAsync();
                    await CreateNewTableAsync(_connection);
                    await InsertRowAsync(_connection);
                    await SelectScalarAsync(_connection);
                    await UpdateRowAsync(_connection);
                    await SelectRecordsAsync(_connection);
                    await DeleteRecordAsync(_connection);
                    _connection.Close();
                }
            }
        }

        private void DeleteRecord(DbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = DeleteCommandText;
                command.AddParameterWithValue("Id", 1);

                int records = _executeNonQuery(command);
                Console.WriteLine($"Deleted {records} record(s).");
            }
        }

        private void SelectRecords(DbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = SelectManyCommandText;
                command.AddParameterWithValue("Id", 1);

                using (var reader = _executeReader(command))
                {
                    var employees = reader.AsDataRecords()
                                          .Select(
                                               r => new { Id = (int)r["Id"], Name = (string)r["Name"] })
                                          .ToList();

                    Console.WriteLine($"Selected {employees.Count} record(s).");
                }

                using (var reader = _executeReaderWithBehavior(command, CommandBehavior.Default))
                {
                    var employees = reader.AsDataRecords()
                                          .Select(
                                               r => new { Id = (int)r["Id"], Name = (string)r["Name"] })
                                          .ToList();

                    Console.WriteLine($"Selected {employees.Count} record(s) with `CommandBehavior.Default`.");
                }
            }
        }

        private void UpdateRow(DbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = UpdateCommandText;
                command.AddParameterWithValue("Name", "Name2");
                command.AddParameterWithValue("Id", 1);

                int records = _executeNonQuery(command);
                Console.WriteLine($"Updated {records} record(s).");
            }
        }

        private void SelectScalar(DbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = SelectOneCommandText;
                command.AddParameterWithValue("Id", 1);

                var name = _executeScalar(command) as string;
                Console.WriteLine($"Selected scalar `{name ?? "(null)"}`.");
            }
        }

        private void InsertRow(DbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = InsertCommandText;
                command.AddParameterWithValue("Id", 1);
                command.AddParameterWithValue("Name", "Name1");

                int records = _executeNonQuery(command);
                Console.WriteLine($"Inserted {records} record(s).");
            }
        }

        private void CreateNewTable(DbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = DropCommandText;

                int records = _executeNonQuery(command);
                Console.WriteLine($"Dropped and recreated table. {records} record(s) affected.");
            }
        }

        private async Task DeleteRecordAsync(DbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = DeleteCommandText;
                command.AddParameterWithValue("Id", 1);

                int records = await _executeNonQueryAsync(command);
                Console.WriteLine($"Deleted {records} record(s).");
            }
        }

        private async Task SelectRecordsAsync(DbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = SelectManyCommandText;
                command.AddParameterWithValue("Id", 1);

                using (var reader = await _executeReaderAsync(command))
                {
                    var employees = reader.AsDataRecords()
                                          .Select(
                                               r => new { Id = (int)r["Id"], Name = (string)r["Name"] })
                                          .ToList();

                    Console.WriteLine($"Selected {employees.Count} record(s).");
                }

                using (var reader = await _executeReaderWithBehaviorAsync(command, CommandBehavior.Default))
                {
                    var employees = reader.AsDataRecords()
                                          .Select(
                                               r => new { Id = (int)r["Id"], Name = (string)r["Name"] })
                                          .ToList();

                    Console.WriteLine($"Selected {employees.Count} record(s) with `CommandBehavior.Default`.");
                }
            }
        }

        private async Task UpdateRowAsync(DbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = UpdateCommandText;
                command.AddParameterWithValue("Name", "Name2");
                command.AddParameterWithValue("Id", 1);

                int records = await _executeNonQueryAsync(command);
                Console.WriteLine($"Updated {records} record(s).");
            }
        }

        private async Task SelectScalarAsync(DbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = SelectOneCommandText;
                command.AddParameterWithValue("Id", 1);

                object nameObj = await _executeScalarAsync(command);
                var name = nameObj as string;
                Console.WriteLine($"Selected scalar `{name ?? "(null)"}`.");
            }
        }

        private async Task InsertRowAsync(DbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = InsertCommandText;
                command.AddParameterWithValue("Id", 1);
                command.AddParameterWithValue("Name", "Name1");

                int records = await _executeNonQueryAsync(command);
                Console.WriteLine($"Inserted {records} record(s).");
            }
        }

        private async Task CreateNewTableAsync(DbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = DropCommandText;

                int records = await _executeNonQueryAsync(command);
                Console.WriteLine($"Dropped and recreated table. {records} record(s) affected.");
            }
        }
    }
}
