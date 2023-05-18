using LinqToDB;
using LinqToDB.Data;
using LinqToDB.Data.DbCommandProcessor;
using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class Linq2DbTests : InstrumentationTestsBase, IDisposable
{
    protected string taintedValue = "brian";
    protected string notTaintedValue = "nottainted";
    string queryUnsafe;
    readonly string querySafe = "SELECT * from Persons where Name like 'Emilio'";
    protected SqlConnection dbConnection;
    TestDb dataConnection;
    DbCommand command;

    public Linq2DbTests()
    {
        AddTainted(taintedValue);
        queryUnsafe = "SELECT * from Persons where Name like '" + taintedValue + "'";
        dbConnection = SqlDDBBCreator.Create();
        command = new SqlCommand(queryUnsafe, dbConnection);
        dataConnection = new TestDb(dbConnection.ConnectionString);
    }

    public void Dispose()
    {
        dataConnection.Close();
        dataConnection = null;
        dbConnection.Close();
        dbConnection = null;
    }

    [Fact]
    public void GivenAVulnerability_WhenGetStack_ThenLocationIsCorrect()
    {
        DbCommandProcessorExtensions.ExecuteReaderExt(command, CommandBehavior.Default);
        AssertLocation(nameof(Linq2DbTests));
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingExecuteReaderExtWithTainted_VulnerabilityIsReported()
    {
        DbCommandProcessorExtensions.ExecuteReaderExt(command, CommandBehavior.Default);
        AssertVulnerable();
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingExecuteReaderExtWithTainted_VulnerabilityIsReported2()
    {
        DbCommandProcessorExtensions.ExecuteReaderExt(new SqlCommand(queryUnsafe, dbConnection, null), CommandBehavior.Default);
        AssertVulnerable();
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingExecuteNonQueryExtWithTainted_VulnerabilityIsReported()
    {
        DbCommandProcessorExtensions.ExecuteNonQueryExt(command);
        AssertVulnerable();
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingExecuteScalarExtWithTainted_VulnerabilityIsReported()
    {
        DbCommandProcessorExtensions.ExecuteScalarExt(command);
        AssertVulnerable();
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingExecuteScalarExtAsyncWithTainted_VulnerabilityIsReported()
    {
        _ = DbCommandProcessorExtensions.ExecuteScalarExtAsync(command, CancellationToken.None).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingExecuteNonQueryExtAsyncWithTainted_VulnerabilityIsReported()
    {
        _ = DbCommandProcessorExtensions.ExecuteNonQueryExtAsync(command, CancellationToken.None).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingExecuteReaderExtAsyncWithTainted_VulnerabilityIsReported()
    {
        _ = DbCommandProcessorExtensions.ExecuteReaderExtAsync(command, CommandBehavior.Default, CancellationToken.None).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingFromSqlWithTainted_VulnerabilityIsReported()
    {
        dataConnection.FromSql<TestDb.Person>(querySafe)?.ToList();
        AssertVulnerable(0);
        dataConnection.FromSql<TestDb.Person>(queryUnsafe)?.ToList();
        AssertVulnerable(1);
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingQueryWithTainted_VulnerabilityIsReported()
    {
        dataConnection.Query<TestDb.Person>(querySafe);
        AssertVulnerable(0);
        dataConnection.Close();
        dataConnection.Query<TestDb.Person>(queryUnsafe);
        AssertVulnerable(1);
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingQueryMultipleWithTainted_VulnerabilityIsReported()
    {
        dataConnection.QueryMultiple<TestDb.Person>(querySafe);
        AssertVulnerable(0);
        dataConnection.QueryMultiple<TestDb.Person>(queryUnsafe);
        AssertVulnerable(1);
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingQueryMultipleAsyncWithTainted_VulnerabilityIsReported()
    {
        dataConnection.QueryMultipleAsync<TestDb.Person>(querySafe).Wait();
        AssertVulnerable(0);
        dataConnection.QueryMultipleAsync<TestDb.Person>(queryUnsafe).Wait();
        AssertVulnerable(1);
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingQueryMultipleAsyncWithTaintedCancellation_VulnerabilityIsReported()
    {
        dataConnection.QueryMultipleAsync<TestDb.Person>(querySafe, CancellationToken.None).Wait();
        AssertVulnerable(0);
        dataConnection.QueryMultipleAsync<TestDb.Person>(queryUnsafe, CancellationToken.None).Wait();
        AssertVulnerable(1);
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingQueryToListAsyncWithTaintedCancellation_VulnerabilityIsReported()
    {
        dataConnection.QueryToListAsync<TestDb.Person>(querySafe, CancellationToken.None).Wait();
        AssertVulnerable(0);
        dataConnection.QueryToListAsync<TestDb.Person>(queryUnsafe, CancellationToken.None).Wait();
        AssertVulnerable(1);
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingQueryToArrayAsyncWithTaintedCancellation_VulnerabilityIsReported()
    {
        dataConnection.QueryToArrayAsync<TestDb.Person>(querySafe, CancellationToken.None).Wait();
        AssertVulnerable(0);
        dataConnection.QueryToArrayAsync<TestDb.Person>(queryUnsafe, CancellationToken.None).Wait();
        AssertVulnerable(1);
    }


    [Fact]
    public void GivenLinq2DbOperation_WhenCallingExecuteWithTainted_VulnerabilityIsReported()
    {
        dataConnection.Execute<TestDb.Person>(querySafe);
        AssertVulnerable(0);
        dataConnection.Execute<TestDb.Person>(queryUnsafe);
        AssertVulnerable(1);
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingExecuteAsyncWithTainted_VulnerabilityIsReported()
    {
        dataConnection.ExecuteAsync<TestDb.Person>(querySafe, CancellationToken.None).Wait();
        AssertVulnerable(0);
        dataConnection.ExecuteAsync<TestDb.Person>(queryUnsafe, CancellationToken.None).Wait();
        AssertVulnerable(1);
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingExecuteReaderWithTainted_VulnerabilityIsReported()
    {
        dataConnection.ExecuteReader(querySafe, CommandBehavior.Default);
        AssertVulnerable(0);
        dataConnection.Close();
        dataConnection.ExecuteReader(queryUnsafe, CommandBehavior.Default);
        AssertVulnerable(1);
    }
}

