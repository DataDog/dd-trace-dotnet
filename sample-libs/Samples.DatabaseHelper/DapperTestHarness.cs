#if !NET45
using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Datadog.Trace;

namespace Samples.DatabaseHelper
{
    public class DapperTestHarness<TConnection>
        where TConnection : class, IDbConnection
    {
        private const string DropCommandText = "DROP TABLE IF EXISTS Employees; CREATE TABLE Employees (Id int PRIMARY KEY, Name varchar(100));";
        private const string InsertCommandText = "INSERT INTO Employees (Id, Name) VALUES (@Id, @Name);";
        private const string SelectOneCommandText = "SELECT Name FROM Employees WHERE Id=@Id;";
        private const string UpdateCommandText = "UPDATE Employees SET Name=@Name WHERE Id=@Id;";
        private const string SelectManyCommandText = "SELECT * FROM Employees WHERE Id=@Id;";
        private const string DeleteCommandText = "DELETE FROM Employees WHERE Id=@Id;";

        private readonly TConnection _connection;
        private readonly string _connectionTypeName;

        public DapperTestHarness(TConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _connectionTypeName = _connection.GetType().FullName;
        }

        public async Task RunAsync()
        {
            using (var scopeAll = Tracer.Instance.StartActive("run.all"))
            {
                scopeAll.Span.SetTag("connection-type", _connectionTypeName);

                using (var scopeSync = Tracer.Instance.StartActive("run.sync"))
                {
                    scopeSync.Span.SetTag("connection-type", _connectionTypeName);

                    _connection.Open();
                    CreateNewTable(_connection);
                    InsertRow(_connection);
                    SelectScalar(_connection);
                    UpdateRow(_connection);
                    SelectRecords(_connection);
                    DeleteRecord(_connection);
                    _connection.Close();
                }

                if (_connection is DbConnection connection)
                {
                    // leave a small space between spans, for better visibility in the UI
                    await Task.Delay(TimeSpan.FromSeconds(0.1));

                    using (var scopeAsync = Tracer.Instance.StartActive("run.async"))
                    {
                        scopeAsync.Span.SetTag("connection-type", _connectionTypeName);

                        await connection.OpenAsync();
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
        }

        private void DeleteRecord(IDbConnection connection)
        {
            int records = connection.Execute(DeleteCommandText, new { Id = 1 });
            Console.WriteLine($"Deleted {records} record(s).");
        }

        private void SelectRecords(IDbConnection connection)
        {
            var command = new CommandDefinition(SelectManyCommandText, new { Id = 1 });

            using (var reader = connection.ExecuteReader(command))
            {
                var employees = reader.AsDataRecords()
                                      .Select(
                                           r => new { Id = (int)r["Id"], Name = (string)r["Name"] })
                                      .ToList();

                Console.WriteLine($"Selected {employees.Count} record(s).");
            }

            using (var reader = connection.ExecuteReader(command, CommandBehavior.Default))
            {
                var employees = reader.AsDataRecords()
                                      .Select(
                                           r => new { Id = (int)r["Id"], Name = (string)r["Name"] })
                                      .ToList();

                Console.WriteLine($"Selected {employees.Count} record(s) with `CommandBehavior.Default`.");
            }
        }

        private void UpdateRow(IDbConnection connection)
        {
            int records = connection.Execute(UpdateCommandText, new { Name = "Name2", Id = 1 });
            Console.WriteLine($"Updated {records} record(s).");
        }

        private void SelectScalar(IDbConnection connection)
        {
            var name = connection.ExecuteScalar(SelectOneCommandText, new { Id = 1 });
            Console.WriteLine($"Selected scalar `{name ?? "(null)"}`.");
        }

        private void InsertRow(IDbConnection connection)
        {
            int records = connection.Execute(InsertCommandText, new { Id = 1, Name = "Name1" });
            Console.WriteLine($"Inserted {records} record(s).");
        }

        private void CreateNewTable(IDbConnection connection)
        {
            int records = connection.Execute(DropCommandText);
            Console.WriteLine($"Dropped and recreated table. {records} record(s) affected.");
        }

        private async Task DeleteRecordAsync(IDbConnection connection)
        {
            int records = await connection.ExecuteAsync(DeleteCommandText, new { Id = 1 });
            Console.WriteLine($"Deleted {records} record(s).");
        }

        private async Task SelectRecordsAsync(IDbConnection connection)
        {
            var command = new CommandDefinition(SelectManyCommandText, new { Id = 1 });

            using (var reader = await connection.ExecuteReaderAsync(command))
            {
                var employees = reader.AsDataRecords()
                                      .Select(
                                           r => new { Id = (int)r["Id"], Name = (string)r["Name"] })
                                      .ToList();

                Console.WriteLine($"Selected {employees.Count} record(s).");
            }

            using (var reader = await connection.ExecuteReaderAsync(command, CommandBehavior.Default))
            {
                var employees = reader.AsDataRecords()
                                      .Select(
                                           r => new { Id = (int)r["Id"], Name = (string)r["Name"] })
                                      .ToList();

                Console.WriteLine($"Selected {employees.Count} record(s) with `CommandBehavior.Default`.");
            }
        }

        private async Task UpdateRowAsync(IDbConnection connection)
        {
            int records = await connection.ExecuteAsync(UpdateCommandText, new { Name = "Name2", Id = 1 });
            Console.WriteLine($"Updated {records} record(s).");
        }

        private async Task SelectScalarAsync(IDbConnection connection)
        {
            var name = await connection.ExecuteScalarAsync<string>(SelectOneCommandText, new { Id = 1 });
            Console.WriteLine($"Selected scalar `{name ?? "(null)"}`.");
        }

        private async Task InsertRowAsync(IDbConnection connection)
        {
            int records = await connection.ExecuteAsync(InsertCommandText, new { Name = "Name1", Id = 1 });
            Console.WriteLine($"Inserted {records} record(s).");
        }

        private async Task CreateNewTableAsync(IDbConnection connection)
        {
            int records = await connection.ExecuteAsync(DropCommandText);
            Console.WriteLine($"Dropped and recreated table. {records} record(s) affected.");
        }
    }
}
#endif
