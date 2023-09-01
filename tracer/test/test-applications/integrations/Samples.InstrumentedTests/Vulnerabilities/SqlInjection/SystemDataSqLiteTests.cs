using System;
using System.Data;
using System.Data.SQLite;
using System.Threading;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

[Trait("Category", "ArmUnsupported")]
public class SystemDataSqLiteTests : InstrumentationTestsBase, IDisposable
{
    protected static string ScalarCommandUnsafe;
    protected static string taintedValue = "Name1";
    static SQLiteConnection dbConnection;
    protected string notTaintedValue = "nottainted";
    string taintedQuery;
    string notTaintedQuery;

    public override void Dispose()
    {
        dbConnection.Close();
        dbConnection.Dispose();
        dbConnection = null;
        base.Dispose();
    }

    public SystemDataSqLiteTests()
    {
        dbConnection = SqliteDDBBCreator.Create() as SQLiteConnection;
        AddTainted(taintedValue);
        taintedQuery = "SELECT * from Persons where name = '" + taintedValue + "'";
        notTaintedQuery = "SELECT * from Persons where name = 'Emilio'";
        ScalarCommandUnsafe = "SELECT Count(*) from Persons where Name like '" + taintedValue + "'";
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported1()
    {
        Assert.Throws<InvalidOperationException>(() => new SQLiteCommand(taintedQuery).ExecuteNonQuery());
        AssertVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported2()
    {
        new SQLiteCommand(taintedQuery, dbConnection, null).ExecuteNonQuery();
        AssertVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported3()
    {
        new SQLiteCommand(taintedQuery, dbConnection).ExecuteNonQuery();
        AssertVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteNonQueryWithNotTainted_VulnerabilityIsNotReported()
    {
        new SQLiteCommand(notTaintedQuery, dbConnection).ExecuteNonQuery();
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteReaderWithTainted_VulnerabilityIsReported()
    {
        new SQLiteCommand(taintedQuery, dbConnection).ExecuteReader();
        AssertVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteReaderWithNotTainted_VulnerabilityIsNotReported()
    {
        new SQLiteCommand(notTaintedQuery, dbConnection).ExecuteReader();
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteReaderCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        new SQLiteCommand(taintedQuery, dbConnection).ExecuteReader(CommandBehavior.Default);
        AssertVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteReaderCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        new SQLiteCommand(notTaintedQuery, dbConnection).ExecuteReader(CommandBehavior.Default);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteScalarWithTainted_VulnerabilityIsReported()
    {
        new SQLiteCommand(taintedQuery, dbConnection).ExecuteScalar();
        AssertVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteScalarWithNotTainted_VulnerabilityIsNotReported()
    {
        new SQLiteCommand(notTaintedQuery, dbConnection).ExecuteScalar();
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteReaderAsyncCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        new SQLiteCommand(taintedQuery, dbConnection).ExecuteReaderAsync(CommandBehavior.Default);
        AssertVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteReaderAsyncCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        new SQLiteCommand(notTaintedQuery, dbConnection).ExecuteReaderAsync(CommandBehavior.Default);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteReaderAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        new SQLiteCommand(taintedQuery, dbConnection).ExecuteReaderAsync(CancellationToken.None);
        AssertVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteReaderAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        //System.Data.SQLite.SQLiteCommand
        new SQLiteCommand(notTaintedQuery, dbConnection).ExecuteReaderAsync(CancellationToken.None);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteReaderAsyncCancellationTokenCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        new SQLiteCommand(taintedQuery, dbConnection).ExecuteReaderAsync(CommandBehavior.Default, CancellationToken.None);
        AssertVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteReaderAsyncCancellationTokenCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        new SQLiteCommand(notTaintedQuery, dbConnection).ExecuteReaderAsync(CommandBehavior.Default, CancellationToken.None);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteReaderAsyncWithTainted_VulnerabilityIsReported()
    {
        new SQLiteCommand(taintedQuery, dbConnection).ExecuteReaderAsync();
        AssertVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteReaderAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        new SQLiteCommand(notTaintedQuery, dbConnection).ExecuteReaderAsync();
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteScalarAsyncWithTainted_VulnerabilityIsReported()
    {
        new SQLiteCommand(taintedQuery, dbConnection).ExecuteScalarAsync();
        AssertVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteScalarAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        new SQLiteCommand(notTaintedQuery, dbConnection).ExecuteScalarAsync();
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteScalarAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        new SQLiteCommand(taintedQuery, dbConnection).ExecuteScalarAsync(CancellationToken.None);
        AssertVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteScalarAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        new SQLiteCommand(notTaintedQuery, dbConnection).ExecuteScalarAsync(CancellationToken.None);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteNonQueryAsyncWithTainted_VulnerabilityIsReported()
    {
        new SQLiteCommand(taintedQuery, dbConnection).ExecuteNonQueryAsync();
        AssertVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteNonQueryAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        new SQLiteCommand(notTaintedQuery, dbConnection).ExecuteNonQueryAsync();
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteNonQueryAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        new SQLiteCommand(taintedQuery, dbConnection).ExecuteNonQueryAsync(CancellationToken.None);
        AssertVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteNonQueryAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        new SQLiteCommand(notTaintedQuery, dbConnection).ExecuteNonQueryAsync(CancellationToken.None);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteReaderCommandBehaviorWithTainted_Tainted()
    {
        var reader = new SQLiteCommand(taintedQuery, dbConnection).ExecuteReader(CommandBehavior.Default);
        reader.Read();
        AssertVulnerable();
    }
}
