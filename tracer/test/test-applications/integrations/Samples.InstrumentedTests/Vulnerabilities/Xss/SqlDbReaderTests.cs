using System;
using System.Data.SqlClient;
using FluentAssertions;
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
}
