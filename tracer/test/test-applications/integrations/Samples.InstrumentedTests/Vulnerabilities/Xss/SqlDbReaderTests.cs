using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;
using Xunit;
using FluentAssertions;
using Remotion.Linq.Clauses.ResultOperators;

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
            reader.Read().Should().BeTrue();
            {
                for (int x = 0; x < reader.FieldCount; x++)
                {
                    if (reader.GetFieldType(x) == typeof(string) && !reader.IsDBNull(x))
                    {
                        var value = reader.GetString(x);
                        AssertTainted(value);
                    }
                }
            }

            reader.Read().Should().BeTrue();
            {
                for (int x = 0; x < reader.FieldCount; x++)
                {
                    if (reader.GetFieldType(x) == typeof(string) && !reader.IsDBNull(x))
                    {
                        var value = reader.GetString(x);
                        AssertNotTainted(value);
                    }
                }
            }

        }
    }

}
