using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace;

namespace Samples.DatabaseHelper
{
    public class RelationalDatabaseTestHarness<TConnection, TCommand, TDataReader>
        where TConnection : class, IDbConnection
        where TCommand : class, IDbCommand
        where TDataReader : class, IDataReader
    {
        private const string DropCommandText = "DROP TABLE IF EXISTS Employees; CREATE TABLE Employees (Id int PRIMARY KEY, Name varchar(100));";
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
        private readonly Func<TCommand, CancellationToken, Task<int>> _executeNonQueryWithCancellationTokenAsync;
        private readonly Func<TCommand, Task<object>> _executeScalarAsync;
        private readonly Func<TCommand, CancellationToken, Task<object>> _executeScalarWithCancellationTokenAsync;
        private readonly Func<TCommand, Task<TDataReader>> _executeReaderAsync;
        private readonly Func<TCommand, CommandBehavior, Task<TDataReader>> _executeReaderWithBehaviorAsync;
        private readonly Func<TCommand, CancellationToken, Task<TDataReader>> _executeReaderWithCancellationTokenAsync;
        private readonly Func<TCommand, CommandBehavior, CancellationToken, Task<TDataReader>> _executeReaderWithBehaviorAndCancellationTokenAsync;

        public RelationalDatabaseTestHarness(
            TConnection connection,
            Func<TCommand, int> executeNonQuery,
            Func<TCommand, object> executeScalar,
            Func<TCommand, TDataReader> executeReader,
            Func<TCommand, CommandBehavior, TDataReader> executeReaderWithBehavior,
            Func<TCommand, Task<int>> executeNonQueryAsync,
            Func<TCommand, CancellationToken, Task<int>> executeNonQueryWithCancellationTokenAsync,
            Func<TCommand, Task<object>> executeScalarAsync,
            Func<TCommand, CancellationToken, Task<object>> executeScalarWithCancellationTokenAsync,
            Func<TCommand, Task<TDataReader>> executeReaderAsync,
            Func<TCommand, CommandBehavior, Task<TDataReader>> executeReaderWithBehaviorAsync,
            Func<TCommand, CancellationToken, Task<TDataReader>> executeReaderWithCancellationTokenAsync,
            Func<TCommand, CommandBehavior, CancellationToken, Task<TDataReader>> executeReaderWithBehaviorAndCancellationTokenAsync)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));

            _executeNonQuery = executeNonQuery ?? throw new ArgumentNullException(nameof(executeNonQuery));
            _executeScalar = executeScalar ?? throw new ArgumentNullException(nameof(executeScalar));
            _executeReader = executeReader ?? throw new ArgumentNullException(nameof(executeReader));
            _executeReaderWithBehavior = executeReaderWithBehavior ?? throw new ArgumentNullException(nameof(executeReaderWithBehavior));

            // async methods are not implemented by all ADO.NET providers, so they can be null
            _executeNonQueryAsync = executeNonQueryAsync;
            _executeNonQueryWithCancellationTokenAsync = executeNonQueryWithCancellationTokenAsync;
            _executeScalarAsync = executeScalarAsync;
            _executeScalarWithCancellationTokenAsync = executeScalarWithCancellationTokenAsync;
            _executeReaderAsync = executeReaderAsync;
            _executeReaderWithBehaviorAsync = executeReaderWithBehaviorAsync;
            _executeReaderWithCancellationTokenAsync = executeReaderWithCancellationTokenAsync;
            _executeReaderWithBehaviorAndCancellationTokenAsync = executeReaderWithBehaviorAndCancellationTokenAsync;
        }

        public async Task RunAsync(string spanName, CancellationToken cancellationToken)
        {
            var commandType = typeof(TCommand).FullName;

            using (var scopeAll = Tracer.Instance.StartActive(spanName))
            {
                scopeAll.Span.SetTag("command-type", commandType);

                using (var scopeSync = Tracer.Instance.StartActive("run.sync"))
                {
                    await Task.Delay(100);
                    scopeSync.Span.SetTag("command-type", commandType);

                    _connection.Open();
                    CreateNewTable(_connection);
                    InsertRow(_connection);
                    SelectScalar(_connection);
                    UpdateRow(_connection);
                    SelectRecords(_connection);
                    SelectRecords(_connection, CommandBehavior.Default);
                    DeleteRecord(_connection);
                    _connection.Close();
                }

                if (_connection is DbConnection connection)
                {
                    await Task.Delay(100);

                    using (var scopeAsync = Tracer.Instance.StartActive("run.async"))
                    {
                        await Task.Delay(100);
                        scopeAsync.Span.SetTag("command-type", commandType);

                        await connection.OpenAsync();

                        await CreateNewTableAsync(_connection);
                        await InsertRowAsync(_connection);
                        await SelectScalarAsync(_connection);
                        await UpdateRowAsync(_connection);
                        await SelectRecordsAsync(_connection);
                        await SelectRecordsAsync(_connection, CommandBehavior.Default);
                        await DeleteRecordAsync(_connection);

                        await Task.Delay(100);

                        await CreateNewTableAsync(_connection, cancellationToken);
                        await InsertRowAsync(_connection, cancellationToken);
                        await SelectScalarAsync(_connection, cancellationToken);
                        await UpdateRowAsync(_connection, cancellationToken);
                        await SelectRecordsAsync(_connection, cancellationToken);
                        await SelectRecordsAsync(_connection, CommandBehavior.Default, cancellationToken);
                        await DeleteRecordAsync(_connection, cancellationToken);

                        _connection.Close();
                    }
                }
            }
        }

        private void DeleteRecord(IDbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = DeleteCommandText;
                command.AddParameterWithValue("Id", 1);

                int records = _executeNonQuery(command);
                Console.WriteLine($"ExecuteNonQuery(). Deleted {records} record(s).");
            }
        }

        private void SelectRecords(IDbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = SelectManyCommandText;
                command.AddParameterWithValue("Id", 1);

                using (var reader = _executeReader(command))
                {
                    var employees = reader.AsDataRecords()
                                          .Select(
                                               r => new
                                                    {
                                                        Id = (int)r["Id"],
                                                        Name = (string)r["Name"]
                                                    })
                                          .ToList();

                    Console.WriteLine($"ExecuteReader(). Selected {employees.Count} record(s).");
                }
            }
        }

        private void SelectRecords(IDbConnection connection, CommandBehavior behavior)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = SelectManyCommandText;
                command.AddParameterWithValue("Id", 1);

                using (var reader = _executeReaderWithBehavior(command, behavior))
                {
                    var employees = reader.AsDataRecords()
                                          .Select(
                                               r => new
                                                    {
                                                        Id = (int)r["Id"],
                                                        Name = (string)r["Name"]
                                                    })
                                          .ToList();

                    Console.WriteLine($"ExecuteReader(CommandBehavior). Selected {employees.Count} record(s).");
                }
            }
        }

        private void UpdateRow(IDbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = UpdateCommandText;
                command.AddParameterWithValue("Name", "Name2");
                command.AddParameterWithValue("Id", 1);

                int records = _executeNonQuery(command);
                Console.WriteLine($"ExecuteNonQuery(). Updated {records} record(s).");
            }
        }

        private void SelectScalar(IDbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = SelectOneCommandText;
                command.AddParameterWithValue("Id", 1);

                var name = _executeScalar(command) as string;
                Console.WriteLine($"ExecuteScalar(). Selected scalar `{name ?? "(null)"}`.");
            }
        }

        private void InsertRow(IDbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = InsertCommandText;
                command.AddParameterWithValue("Id", 1);
                command.AddParameterWithValue("Name", "Name1");

                int records = _executeNonQuery(command);
                Console.WriteLine($"ExecuteNonQuery(). Inserted {records} record(s).");
            }
        }

        private void CreateNewTable(IDbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = DropCommandText;

                int records = _executeNonQuery(command);

                Console.WriteLine($"ExecuteNonQuery(). Dropped and recreated table. {records} record(s) affected.");
            }
        }

        private async Task DeleteRecordAsync(IDbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = DeleteCommandText;
                command.AddParameterWithValue("Id", 1);

                if (_executeNonQueryAsync != null)
                {
                    int records = await _executeNonQueryAsync(command);
                    Console.WriteLine($"ExecuteNonQueryAsync(). Deleted {records} record(s).");
                }
            }
        }

        private async Task DeleteRecordAsync(IDbConnection connection, CancellationToken cancellationToken)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = DeleteCommandText;
                command.AddParameterWithValue("Id", 1);

                if (_executeNonQueryWithCancellationTokenAsync != null)
                {
                    int records = await _executeNonQueryWithCancellationTokenAsync(command, cancellationToken);
                    Console.WriteLine($"ExecuteNonQueryAsync(CancellationToken). Deleted {records} record(s).");
                }
            }
        }

        private async Task SelectRecordsAsync(IDbConnection connection)
        {
            if (_executeReaderAsync != null)
            {
                await SelectRecordsAsync(connection, "ExecuteReaderAsync()", command => _executeReaderAsync(command));
            }
        }

        private async Task SelectRecordsAsync(IDbConnection connection, CommandBehavior behavior)
        {
            if (_executeReaderWithBehaviorAsync != null)
            {
                await SelectRecordsAsync(connection, "ExecuteReaderAsync(CommandBehavior)", command => _executeReaderWithBehaviorAsync(command, behavior));
            }
        }

        private async Task SelectRecordsAsync(IDbConnection connection, CancellationToken cancellationToken)
        {
            if (_executeReaderWithCancellationTokenAsync != null)
            {
                await SelectRecordsAsync(connection, "ExecuteReaderAsync(CancellationToken)", command => _executeReaderWithCancellationTokenAsync(command, cancellationToken));
            }
        }

        private async Task SelectRecordsAsync(IDbConnection connection, CommandBehavior behavior, CancellationToken cancellationToken)
        {
            if (_executeReaderWithBehaviorAndCancellationTokenAsync != null)
            {
                await SelectRecordsAsync(connection, "ExecuteReaderAsync(CommandBehavior, CancellationToken)", command => _executeReaderWithBehaviorAndCancellationTokenAsync(command, behavior, cancellationToken));
            }
        }

        private async Task SelectRecordsAsync(IDbConnection connection, string method, Func<TCommand, Task<TDataReader>> func)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = SelectManyCommandText;
                command.AddParameterWithValue("Id", 1);

                using (var reader = await func(command))
                {
                    var employees = reader.AsDataRecords()
                                          .Select(
                                               r => new
                                                    {
                                                        Id = (int)r["Id"],
                                                        Name = (string)r["Name"]
                                                    })
                                          .ToList();

                    Console.WriteLine($"{method}. Selected {employees.Count} record(s).");
                }
            }
        }

        private async Task UpdateRowAsync(IDbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = UpdateCommandText;
                command.AddParameterWithValue("Name", "Name2");
                command.AddParameterWithValue("Id", 1);

                if (_executeNonQueryAsync != null)
                {
                    int records = await _executeNonQueryAsync(command);
                    Console.WriteLine($"ExecuteNonQueryAsync(). Updated {records} record(s).");
                }
            }
        }

        private async Task UpdateRowAsync(IDbConnection connection, CancellationToken cancellationToken)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = UpdateCommandText;
                command.AddParameterWithValue("Name", "Name2");
                command.AddParameterWithValue("Id", 1);

                if (_executeNonQueryWithCancellationTokenAsync != null)
                {
                    int records = await _executeNonQueryWithCancellationTokenAsync(command, cancellationToken);
                    Console.WriteLine($"ExecuteNonQueryAsync(CancellationToken). Updated {records} record(s).");
                }
            }
        }

        private async Task SelectScalarAsync(IDbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = SelectOneCommandText;
                command.AddParameterWithValue("Id", 1);

                if (_executeScalarAsync != null)
                {
                    object nameObj = await _executeScalarAsync(command);
                    var name = nameObj as string ?? "(null)";
                    Console.WriteLine($"ExecuteScalarAsync(). Selected scalar `{name}`.");
                }
            }
        }

        private async Task SelectScalarAsync(IDbConnection connection, CancellationToken cancellationToken)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = SelectOneCommandText;
                command.AddParameterWithValue("Id", 1);

                if (_executeScalarWithCancellationTokenAsync != null)
                {
                    object nameObj = await _executeScalarWithCancellationTokenAsync(command, cancellationToken);
                    var name = nameObj as string ?? "(null)";
                    Console.WriteLine($"ExecuteScalarAsync(CancellationToken). Selected scalar `{name}`.");
                }
            }
        }

        private async Task InsertRowAsync(IDbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = InsertCommandText;
                command.AddParameterWithValue("Id", 1);
                command.AddParameterWithValue("Name", "Name1");

                if (_executeNonQueryAsync != null)
                {
                    int records = await _executeNonQueryAsync(command);
                    Console.WriteLine($"ExecuteNonQueryAsync(). Inserted {records} record(s).");
                }
            }
        }

        private async Task InsertRowAsync(IDbConnection connection, CancellationToken cancellationToken)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = InsertCommandText;
                command.AddParameterWithValue("Id", 1);
                command.AddParameterWithValue("Name", "Name1");

                if (_executeNonQueryWithCancellationTokenAsync != null)
                {
                    int records = await _executeNonQueryWithCancellationTokenAsync(command, cancellationToken);
                    Console.WriteLine($"ExecuteNonQueryAsync(CancellationToken). Inserted {records} record(s).");
                }
            }
        }

        private async Task CreateNewTableAsync(IDbConnection connection)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = DropCommandText;

                if (_executeNonQueryAsync != null)
                {
                    int records = await _executeNonQueryAsync(command);
                    Console.WriteLine($"ExecuteNonQueryAsync(). Dropped and recreated table. {records} record(s) affected.");
                }
            }
        }

        private async Task CreateNewTableAsync(IDbConnection connection, CancellationToken cancellationToken)
        {
            using (var command = (TCommand)connection.CreateCommand())
            {
                command.CommandText = DropCommandText;

                if (_executeNonQueryWithCancellationTokenAsync != null)
                {
                    int records = await _executeNonQueryWithCancellationTokenAsync(command, cancellationToken);
                    Console.WriteLine($"ExecuteNonQueryAsync(CancellationToken). Dropped and recreated table. {records} record(s) affected.");
                }
            }
        }
    }
}
