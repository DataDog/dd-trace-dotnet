using LinqToDB.Data;
using LinqToDB.Data.DbCommandProcessor;
using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

// We cannot use localDB on linux and these calls cannot be mocked
[Trait("Category", "LinuxUnsupported")]
public class Linq2DbTests : InstrumentationTestsBase, IDisposable
{
    protected string taintedValue = "brian";
    protected string notTaintedValue = "nottainted";
    string queryUnsafe;
    readonly string querySafe = "SELECT * from Persons where Name like 'Emilio'";
    protected SqlConnection dbConnection;
    TestDb dataConnection;
    DbCommand commandUnsafe;

    public Linq2DbTests()
    {
        AddTainted(taintedValue);
        queryUnsafe = "SELECT * from Persons where Name like '" + taintedValue + "'";
        dbConnection = SqlDDBBCreator.Create();
        commandUnsafe = new SqlCommand(queryUnsafe, dbConnection);
        dataConnection = new TestDb(dbConnection.ConnectionString);
    }

    public override void Dispose()
    {
        dataConnection.Close();
        dataConnection = null;
        dbConnection.Close();
        dbConnection = null;
        base.Dispose();
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingExecuteReaderExtWithTainted_VulnerabilityIsReported()
    {
        DbCommandProcessorExtensions.ExecuteReaderExt(commandUnsafe, CommandBehavior.Default);
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
        DbCommandProcessorExtensions.ExecuteNonQueryExt(commandUnsafe);
        AssertVulnerable();
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingExecuteScalarExtWithTainted_VulnerabilityIsReported()
    {
        DbCommandProcessorExtensions.ExecuteScalarExt(commandUnsafe);
        AssertVulnerable();
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingExecuteScalarExtAsyncWithTainted_VulnerabilityIsReported()
    {
        _ = DbCommandProcessorExtensions.ExecuteScalarExtAsync(commandUnsafe, CancellationToken.None).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingExecuteNonQueryExtAsyncWithTainted_VulnerabilityIsReported()
    {
        _ = DbCommandProcessorExtensions.ExecuteNonQueryExtAsync(commandUnsafe, CancellationToken.None).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingExecuteReaderExtAsyncWithTainted_VulnerabilityIsReported()
    {
        _ = DbCommandProcessorExtensions.ExecuteReaderExtAsync(commandUnsafe, CommandBehavior.Default, CancellationToken.None).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingQueryWithTainted_VulnerabilityIsReported()
    {
        dataConnection.Query<TestDb.Person>(querySafe);
        AssertNotVulnerable();
        dataConnection.Close();
        dataConnection.Query<TestDb.Person>(queryUnsafe);
        AssertVulnerable();
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingQueryMultipleWithTainted_VulnerabilityIsReported()
    {
        dataConnection.QueryMultiple<TestDb.Person>(querySafe);
        AssertNotVulnerable();
        dataConnection.QueryMultiple<TestDb.Person>(queryUnsafe);
        AssertVulnerable();
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingQueryMultipleAsyncWithTainted_VulnerabilityIsReported()
    {
        dataConnection.QueryMultipleAsync<TestDb.Person>(querySafe).Wait();
        AssertNotVulnerable();
        dataConnection.QueryMultipleAsync<TestDb.Person>(queryUnsafe).Wait();
        AssertVulnerable();
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingQueryMultipleAsyncWithTaintedCancellation_VulnerabilityIsReported()
    {
        dataConnection.QueryMultipleAsync<TestDb.Person>(querySafe, CancellationToken.None).Wait();
        AssertNotVulnerable();
        dataConnection.QueryMultipleAsync<TestDb.Person>(queryUnsafe, CancellationToken.None).Wait();
        AssertVulnerable();
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingQueryToListAsyncWithTaintedCancellation_VulnerabilityIsReported()
    {
        dataConnection.QueryToListAsync<TestDb.Person>(querySafe, CancellationToken.None).Wait();
        AssertNotVulnerable();
        dataConnection.QueryToListAsync<TestDb.Person>(queryUnsafe, CancellationToken.None).Wait();
        AssertVulnerable();
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingQueryToArrayAsyncWithTaintedCancellation_VulnerabilityIsReported()
    {
        dataConnection.QueryToArrayAsync<TestDb.Person>(querySafe, CancellationToken.None).Wait();
        AssertNotVulnerable();
        dataConnection.QueryToArrayAsync<TestDb.Person>(queryUnsafe, CancellationToken.None).Wait();
        AssertVulnerable();
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingExecuteWithTainted_VulnerabilityIsReported()
    {
        dataConnection.Execute<TestDb.Person>(querySafe);
        AssertNotVulnerable();
        dataConnection.Execute<TestDb.Person>(queryUnsafe);
        AssertVulnerable();
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingExecuteAsyncWithTainted_VulnerabilityIsReported()
    {
        dataConnection.ExecuteAsync<TestDb.Person>(querySafe, CancellationToken.None).Wait();
        AssertNotVulnerable();
        dataConnection.ExecuteAsync<TestDb.Person>(queryUnsafe, CancellationToken.None).Wait();
        AssertVulnerable();
    }

    [Fact]
    public void GivenLinq2DbOperation_WhenCallingExecuteReaderWithTainted_VulnerabilityIsReported()
    {
        dataConnection.ExecuteReader(querySafe, CommandBehavior.Default);
        AssertNotVulnerable();
        dataConnection.Close();
        dataConnection.ExecuteReader(queryUnsafe, CommandBehavior.Default);
        AssertVulnerable();
    }
}

