using System.Data;

namespace Samples.DatabaseHelper
{
    public class DbCommandFactory
    {
        private string _quotedTableName;
        private readonly string _quotedPrimaryKeyName;

        public string QuotedTableName
        {
            get => _quotedTableName;
            set
            {
                _quotedTableName = value;
            }
        }

        public DbCommandFactory(string quotedTableName, string quotedPrimaryKeyName = null)
        {
            _quotedTableName = quotedTableName;
            // SQL Server samples supply a table-derived name to avoid collisions between auto-generated constraint names.
            _quotedPrimaryKeyName = quotedPrimaryKeyName;
        }

        public virtual IDbCommand GetCreateTableCommand(IDbConnection connection, IDbTransaction transaction = null)
        {
            var command = connection.CreateCommand();
            if (transaction != null)
            {
                command.Transaction = transaction;
            }
            var primaryKeyConstraint = _quotedPrimaryKeyName is null ? string.Empty : $"CONSTRAINT {_quotedPrimaryKeyName} ";
            command.CommandText = $"DROP TABLE IF EXISTS {_quotedTableName}; CREATE TABLE {_quotedTableName} (Id int {primaryKeyConstraint}PRIMARY KEY, Name varchar(100));";
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
