using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using Samples.DatabaseHelper;

namespace Samples.OracleMDA
{
    internal static class Program
    {
        private static string Host => Environment.GetEnvironmentVariable("ORACLE_HOST") ?? "localhost";

        private static async Task Main()
        {
            var tableId = Guid.NewGuid().ToString("N").Substring(0, 10);
            var commandFactory = new OracleDbCommandFactory($@"oracletest{tableId}");
            var commandExecutor = new OracleCommandExecutor();

            using (var connection = OpenConnection())
            {
                await RelationalDatabaseTestHarness.RunAllAsync<OracleCommand>(connection, commandFactory, commandExecutor, CancellationToken.None);
            }

            // allow time to flush
            await Task.Delay(2000);
        }

        private static OracleConnection OpenConnection()
        {
            var cstringBuilder = new OracleConnectionStringBuilder();
            cstringBuilder.DataSource = $"{Host}:1521/FREE";
            cstringBuilder.UserID = "system";
            cstringBuilder.Password = "testpassword";
            
            var connection = new OracleConnection(cstringBuilder.ConnectionString);
            connection.Open();
            return connection;
        }
        
        class OracleDbCommandFactory : DbCommandFactory
        {
            private string _qTableName = null;
            private int _count = 0;
            
            public OracleDbCommandFactory(string quotedTableName)
                : base(quotedTableName)
            {
                _qTableName = quotedTableName;
            }

            public override IDbCommand GetCreateTableCommand(IDbConnection connection)
            {
                var command = connection.CreateCommand();
                command.CommandText = $"create table {QuotedTableName} (Id number(10) not null, Name varchar2(100) not null)";
                return command;
            }

            public override IDbCommand GetInsertRowCommand(IDbConnection connection)
            {
                var command = connection.CreateCommand();
                command.CommandText = $"INSERT INTO {QuotedTableName} (Id, Name) VALUES (:Id, :Name)";
                command.AddParameterWithValue("Id", 1);
                command.AddParameterWithValue("Name", "Name1");
                return command;
            }
            
            public override IDbCommand GetSelectScalarCommand(IDbConnection connection)
            {
                var command = connection.CreateCommand();
                command.CommandText = $"SELECT Name FROM {QuotedTableName} WHERE Id=:Id";
                command.AddParameterWithValue("Id", 1);
                return command;
            }
            
            public override IDbCommand GetUpdateRowCommand(IDbConnection connection)
            {
                var command = connection.CreateCommand();
                command.CommandText = $"UPDATE {QuotedTableName} SET Name=:Name WHERE Id=:Id";
                command.AddParameterWithValue("Name", "Name2");
                command.AddParameterWithValue("Id", 1);
                return command;
            }
            
            public override IDbCommand GetSelectRowCommand(IDbConnection connection)
            {
                var command = connection.CreateCommand();
                command.CommandText = $"SELECT * FROM {QuotedTableName} WHERE Id=:Id";
                command.AddParameterWithValue("Id", 1);
                return command;
            }
            
            public override IDbCommand GetDeleteRowCommand(IDbConnection connection)
            {
                var command = connection.CreateCommand();
                command.CommandText = $"DELETE FROM {QuotedTableName} WHERE Id=:Id";
                command.AddParameterWithValue("Id", 1);
                QuotedTableName = _qTableName + (_count++);
                return command;
            }
        }
    }
}
