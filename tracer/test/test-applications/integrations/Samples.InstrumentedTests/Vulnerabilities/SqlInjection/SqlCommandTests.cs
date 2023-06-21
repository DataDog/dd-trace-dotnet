using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class SqlCommandTests : InstrumentationTestsBase, IDisposable
{
    protected string taintedValue = "tainted";
    protected string notTaintedValue = "nottainted";
    string taintedQuery;
    string notTaintedQuery;

    SqlConnection databaseConnection;

    public SqlCommandTests()
    {
        databaseConnection = SqlDDBBCreator.Create();
        AddTainted(taintedValue);
        taintedQuery = "SELECT * from persons where name = '" + taintedValue + "'";
        notTaintedQuery = "SELECT * from persons where name = 'Emilio'";
    }

    public void Dispose()
    {
        if (databaseConnection != null)
        {
            databaseConnection.Close();
            databaseConnection.Dispose();
            databaseConnection = null;
        }
    }
}
