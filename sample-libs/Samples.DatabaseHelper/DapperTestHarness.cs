#if !NET45
using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Datadog.Trace;
using Dapper;

namespace Samples.DatabaseHelper
{
    public class DapperTestHarness<TConnection, TCommand, TDataReader>
        where TConnection : class, IDbConnection
        where TCommand : class, IDbCommand
        where TDataReader : class, IDataReader

    {
        // These sql strings are passed through a Query object on the connection object (see below)
        // which is how Dapper shortcuts some of the steps for their users.
        private const string DropCommandText = "DROP TABLE IF EXISTS Employees; CREATE TABLE Employees (Id int PRIMARY KEY, Name varchar(100));";
        private const string InsertCommandText = "INSERT INTO Employees (Id, Name) VALUES (1, 'nametest');";
        private const string SelectOneCommandText = "SELECT Name FROM Employees WHERE Id=1;";
        private const string SelectManyCommandText = "SELECT * FROM Employees WHERE Id=1;";

        private readonly TConnection _connection;

        public DapperTestHarness(
            TConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public async Task RunAsync()
        {
            using (var scopeAll = Tracer.Instance.StartActive("run.all"))
            {
                scopeAll.Span.SetTag("command-type", typeof(TCommand).FullName);

                using (var scopeSync = Tracer.Instance.StartActive("run.sync"))
                {
                    scopeSync.Span.SetTag("command-type", typeof(TCommand).FullName);

                    _connection.Open();
                    SelectRecords(_connection);
                    _connection.Close();
                }
            }

            if (_connection is DbConnection connection)
            {
                // leave a small space between spans, for better visibility in the UI
                await Task.Delay(TimeSpan.FromSeconds(0.1));

                using (var scopeAsync = Tracer.Instance.StartActive("run.async"))
                {
                    scopeAsync.Span.SetTag("command-type", typeof(TCommand).FullName);

                    await connection.OpenAsync();
                    await SelectRecordsAsync(_connection);
                    _connection.Close();
                }
            }

        }

        private void SelectRecords(IDbConnection connection)
        {
            connection.Execute(DropCommandText);
            connection.Execute(InsertCommandText);

            // Dapper has its own unique way of passing a query.
            connection.Query(SelectOneCommandText);
        }

        private async Task SelectRecordsAsync(IDbConnection connection)
        {
            await connection.ExecuteAsync(SelectManyCommandText);
        }

    }
}
#endif
