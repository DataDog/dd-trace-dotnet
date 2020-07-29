using System.Data;

namespace Samples.DatabaseHelper
{
    public class DbCommandFactory
    {
        public IDbCommand GetCreateTableCommand(IDbConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = "DROP TABLE IF EXISTS Employees; CREATE TABLE Employees (Id int PRIMARY KEY, Name varchar(100));";
            return command;
        }

        public IDbCommand GetInsertRowCommand(IDbConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Employees (Id, Name) VALUES (@Id, @Name);";
            command.AddParameterWithValue("Id", 1);
            command.AddParameterWithValue("Name", "Name1");
            return command;
        }

        public IDbCommand GetUpdateRowCommand(IDbConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Employees SET Name=@Name WHERE Id=@Id;";
            command.AddParameterWithValue("Name", "Name2");
            command.AddParameterWithValue("Id", 1);
            return command;
        }

        public IDbCommand GetSelectScalarCommand(IDbConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Name FROM Employees WHERE Id=@Id;";
            command.AddParameterWithValue("Id", 1);
            return command;
        }

        public IDbCommand GetSelectRowCommand(IDbConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Employees WHERE Id=@Id;";
            command.AddParameterWithValue("Id", 1);
            return command;
        }

        public IDbCommand GetDeleteRowCommand(IDbConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Employees WHERE Id=@Id;";
            command.AddParameterWithValue("Id", 1);
            return command;
        }
    }
}
