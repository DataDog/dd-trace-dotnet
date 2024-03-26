using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

namespace Samples.DatabaseHelper
{
    public class DapperTestHarness
    {
        private const string DropCommandText = "DROP TABLE IF EXISTS DapperEmployees; CREATE TABLE DapperEmployees (Id int PRIMARY KEY, Name varchar(100));";
        private const string InsertCommandText = "INSERT INTO DapperEmployees (Id, Name) VALUES (@Id, @Name);";
        private const string SelectOneCommandText = "SELECT Name FROM DapperEmployees WHERE Id=@Id;";
        private const string UpdateCommandText = "UPDATE DapperEmployees SET Name=@Name WHERE Id=@Id;";
        private const string SelectManyCommandText = "SELECT * FROM DapperEmployees WHERE Id=@Id;";
        private const string DeleteCommandText = "DELETE FROM DapperEmployees WHERE Id=@Id;";

        public async Task RunAsync(DbConnection connection)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            string connectionTypeName = connection.GetType().FullName;

            using (var scopeAll = SampleHelpers.CreateScope("run.all"))
            {
                SampleHelpers.TrySetTag(scopeAll, "connection-type", connectionTypeName);

                using (var scopeSync = SampleHelpers.CreateScope("run.sync"))
                {
                    SampleHelpers.TrySetTag(scopeSync, "connection-type", connectionTypeName);

                    connection.Open();
                    CreateNewTable(connection);
                    InsertRow(connection);
                    SelectScalar(connection);
                    Query(connection);
                    UpdateRow(connection);
                    SelectRecords(connection);
                    DeleteRecord(connection);
                    connection.Close();
                }

                // leave a small space between spans, for better visibility in the UI
                await Task.Delay(TimeSpan.FromSeconds(0.1));

                using (var scopeAsync = SampleHelpers.CreateScope("run.async"))
                {
                    SampleHelpers.TrySetTag(scopeAsync, "connection-type", connectionTypeName);

                    await connection.OpenAsync();
                    await CreateNewTableAsync(connection);
                    await InsertRowAsync(connection);
                    await SelectScalarAsync(connection);
                    await QueryAsync(connection);
                    await UpdateRowAsync(connection);
                    await SelectRecordsAsync(connection);
                    await DeleteRecordAsync(connection);
                    connection.Close();
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

        private void Query(IDbConnection connection)
        {
            var employees = connection.Query(SelectManyCommandText, new { Id = 1 }).ToList();
            Console.WriteLine($"Selected {employees.Count} record(s) with Query().");
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

        private async Task QueryAsync(IDbConnection connection)
        {
            var employees = (await connection.QueryAsync(SelectManyCommandText, new { Id = 1 })).ToList();
            Console.WriteLine($"Selected {employees.Count} record(s) with Query().");
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
