using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;
using Xunit;

#nullable enable

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.Xss;

public class SqlDbReaderTests : InstrumentationTestsBase, IDisposable
{
    string allPersonsQuery;

    SqlConnection databaseConnection;

    public SqlDbReaderTests()
    {
        databaseConnection = SqlDDBBCreator.Create();
        allPersonsQuery = "SELECT * from Persons";
    }

    public override void Dispose()
    {
        if (databaseConnection != null)
        {
            databaseConnection.Close();
            databaseConnection.Dispose();
            databaseConnection = null;
        }
        base.Dispose();
    }

    [Fact]
    public void GivenASqlReader_WhenCallingGetString_OutputIsTainted()
    {
        using (var reader = TestRealDDBBLocalCall(() => new SqlCommand(allPersonsQuery, databaseConnection).ExecuteReader()))
        {
            while (reader.Read())
            {
                for (int x = 0; x < reader.FieldCount; x++)
                {
                    var value = reader.GetString(x);
                    AssertTainted(value);
                    break;
                }
                break;
            }
        }
    }

}
