using System.Data.Common;
using System.Data.SqlClient;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class SqlServerTests : DatabaseInstrumentationTestsBase
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
}
