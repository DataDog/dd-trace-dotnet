using System.Data.Common;
using System.Data.SqlClient;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class SqlServerMicrosoftDataSqlClientTests : DatabaseInstrumentationTestsBase
{
    DbConnection connection;
    protected override string GetConnectionString()
    {
        connection = SqlDDBBCreator.Create();
        return connection.ConnectionString;
    }

    protected override DbConnection GetDbConnection(string connectionString)
    {
        return connection;
    }

    protected override DbCommand GetCommand(string query, DbConnection connection)
    {
        return new SqlCommand(query, (SqlConnection)connection);
    }

    protected override DbCommand GetCommand(string query)
    {
        return new SqlCommand(query);
    }

    protected override DbCommand GetCommand()
    {
        return new SqlCommand();
    }

    private SqlConnection OpenSqlConnection()
    {
        return (SqlConnection)OpenConnection();
    }

    protected DbCommand DbCommand(DbConnection dbConnection, string commandTxt, object param = null)
    {
        var dbCommand = dbConnection.CreateCommand();
        dbCommand.CommandText = commandTxt;
        if (param != null)
        {
            dbCommand.Parameters.Add(param);
        }
        return dbCommand;
    }

    protected DbDataReader DataReader(DbConnection dbConnection)
    {
        return DbCommand(dbConnection, "Select * From Persons").ExecuteReader();
    }
}
