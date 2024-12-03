using System.Data;

namespace Samples.DatabaseHelper
{
    public class DbCommandFactory
    {
        private string _quotedTableName;

        public string QuotedTableName
        {
            get => _quotedTableName;
            set
            {
                _quotedTableName = value;
            }
        }

        public DbCommandFactory(string quotedTableName)
        {
            _quotedTableName = quotedTableName;
        }

        public virtual IDbCommand GetCreateTableCommand(IDbConnection connection, IDbTransaction transaction = null)
        {
            var command = connection.CreateCommand();
            if (transaction != null)
            {
                command.Transaction = transaction;
            }
            command.CommandText = $"DROP TABLE IF EXISTS {_quotedTableName}; CREATE TABLE {_quotedTableName} (Id int PRIMARY KEY, Name varchar(100));";
            return command;
        }

        public virtual IDbCommand GetInsertRowCommand(IDbConnection connection, IDbTransaction transaction = null)
        {
            var command = connection.CreateCommand();
            if (transaction != null)
            {
                command.Transaction = transaction;
            }
            command.CommandText = $"INSERT INTO {_quotedTableName} (Id, Name) VALUES (@Id, @Name);";
            command.AddParameterWithValue("Id", 1);
            command.AddParameterWithValue("Name", "Name1");
            return command;
        }

        public virtual IDbCommand GetUpdateRowCommand(IDbConnection connection, IDbTransaction transaction = null)
        {
            var command = connection.CreateCommand();
            if (transaction != null)
            {
                command.Transaction = transaction;
            }
            command.CommandText = $"UPDATE {_quotedTableName} SET Name=@Name WHERE Id=@Id;";
            command.AddParameterWithValue("Name", "Name2");
            command.AddParameterWithValue("Id", 1);
            return command;
        }

        public virtual IDbCommand GetSelectScalarCommand(IDbConnection connection, IDbTransaction transaction = null)
        {
            var command = connection.CreateCommand();
            if (transaction != null)
            {
                command.Transaction = transaction;
            }
            command.CommandText = $"SELECT Name FROM {_quotedTableName} WHERE Id=@Id;";
            command.AddParameterWithValue("Id", 1);
            return command;
        }

        public virtual IDbCommand GetSelectRowCommand(IDbConnection connection, IDbTransaction transaction = null)
        {
            var command = connection.CreateCommand();
            if (transaction != null)
            {
                command.Transaction = transaction;
            }
            command.CommandText = $"SELECT * FROM {_quotedTableName} WHERE Id=@Id;";
            command.AddParameterWithValue("Id", 1);
            return command;
        }

        public virtual IDbCommand GetDeleteRowCommand(IDbConnection connection, IDbTransaction transaction = null)
        {
            var command = connection.CreateCommand();
            if (transaction != null)
            {
                command.Transaction = transaction;
            }
            command.CommandText = $"DELETE FROM {_quotedTableName} WHERE Id=@Id;";
            command.AddParameterWithValue("Id", 1);
            return command;
        }
    }
}
