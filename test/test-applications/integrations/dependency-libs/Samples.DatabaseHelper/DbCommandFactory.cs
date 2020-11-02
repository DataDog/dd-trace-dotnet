using System.Data;

namespace Samples.DatabaseHelper
{
    public class DbCommandFactory
    {
        private readonly string _tableName;

        public DbCommandFactory(string tableName)
        {
            _tableName = tableName;
        }

        public IDbCommand GetCreateTableCommand(IDbConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS {_tableName}; CREATE TABLE {_tableName} (Id int PRIMARY KEY, Name varchar(100));";
            return command;
        }

        public IDbCommand GetInsertRowCommand(IDbConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = $"INSERT INTO {_tableName} (Id, Name) VALUES (@Id, @Name);";
            command.AddParameterWithValue("Id", 1);
            command.AddParameterWithValue("Name", "Name1");
            return command;
        }

        public IDbCommand GetUpdateRowCommand(IDbConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = $"UPDATE {_tableName} SET Name=@Name WHERE Id=@Id;";
            command.AddParameterWithValue("Name", "Name2");
            command.AddParameterWithValue("Id", 1);
            return command;
        }

        public IDbCommand GetSelectScalarCommand(IDbConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = $"SELECT Name FROM {_tableName} WHERE Id=@Id;";
            command.AddParameterWithValue("Id", 1);
            return command;
        }

        public IDbCommand GetSelectRowCommand(IDbConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM {_tableName} WHERE Id=@Id;";
            command.AddParameterWithValue("Id", 1);
            return command;
        }

        public IDbCommand GetDeleteRowCommand(IDbConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM {_tableName} WHERE Id=@Id;";
            command.AddParameterWithValue("Id", 1);
            return command;
        }
    }
}
