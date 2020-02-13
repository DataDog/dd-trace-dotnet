#if !NET452

using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using Datadog.Trace;

namespace Samples.Dapper
{
    internal static class Program
    {
        private static async Task Main()
        {
            using (var connection = CreateConnection())
            {
                var testQueries = new DapperTestHarness<IDbConnection, IDbCommand, IDataReader>(
                    connection,
                    command => command.ExecuteNonQuery(),
                    command => command.ExecuteScalar(),
                    command => command.ExecuteReader(),
                    (command, behavior) => command.ExecuteReader(behavior),
                    executeNonQueryAsync: null,
                    executeScalarAsync: null,
                    executeReaderAsync: null,
                    executeReaderWithBehaviorAsync: null
                );

                await testQueries.RunAsync();
            }
        }

        private static NpgsqlConnection CreateConnection()
        {
            var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");

            if (connectionString == null)
            {
                var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
                connectionString = $"Host={host};Username=postgres;Password=postgres;Database=postgres";
            }

            return new NpgsqlConnection(connectionString);
        }
    }

    public class DapperTestHarness<TConnection, TCommand, TDataReader>
        where TConnection : class, IDbConnection
        where TCommand : class, IDbCommand
        where TDataReader : class, IDataReader

    {
        private const string DropCommandText = "DROP TABLE IF EXISTS Employees; CREATE TABLE Employees (Id int PRIMARY KEY, Name varchar(100));";
        private const string InsertCommandText = "INSERT INTO Employees (Id, Name) VALUES (1, 'nametest');";
        private const string SelectOneCommandText = "SELECT Name FROM Employees WHERE Id=1;";
        private const string SelectManyCommandText = "SELECT * FROM Employees WHERE Id=1;";

        private readonly TConnection _connection;

        public DapperTestHarness(
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
        }

        public async Task RunAsync()
        {
            //using (var scopeAll = Tracer.Instance.StartActive("run.all"))
            //{
            //    scopeAll.Span.SetTag("command-type", typeof(TCommand).FullName);

            //    using (var scopeSync = Tracer.Instance.StartActive("run.sync"))
            //    {
            //        scopeSync.Span.SetTag("command-type", typeof(TCommand).FullName);

            //        _connection.Open();
            //        SelectRecords(_connection);
            //        _connection.Close();
            //    }
            //}

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
#else
namespace Samples.Dapper
{
    internal static class Program
    {
        private static void Main()
        { }
    }
}
#endif

