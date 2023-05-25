using System;
using System.Data;
using System.Threading;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class MicrosoftSqLiteTests : InstrumentationTestsBase, IDisposable
{
    protected static string ScalarCommandUnsafe;
    protected static string taintedValue = "Name1";
    static SqliteConnection dbConnection;
    protected string notTaintedValue = "nottainted";
    string taintedQuery;
    string notTaintedQuery;

    public void Dispose()
    {
        dbConnection.Close();
        dbConnection.Dispose();
        dbConnection = null;
    }

    public MicrosoftSqLiteTests()
    {
        dbConnection = MicrosoftDataSqliteDdbbCreator.Create();
        AddTainted(taintedValue);
        taintedQuery = "SELECT * from persons where name = '" + taintedValue + "'";
        notTaintedQuery = "SELECT * from persons where name = 'Emilio'";
        ScalarCommandUnsafe = "SELECT Count(*) from Persons where Name like '" + taintedValue + "'";
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported()
    {
        new SqliteCommand(taintedQuery, dbConnection).ExecuteNonQuery();
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported2()
    {
        new SqliteCommand(taintedQuery, dbConnection, null).ExecuteNonQuery();
        AssertVulnerable();
    }

#if !NETFRAMEWORK
    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteNonQueryWithNull_VulnerabilityIsReported()
    {
        Assert.Throws<InvalidOperationException>(() => new SqliteCommand(null, dbConnection).ExecuteNonQuery());
    }
#endif

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteNonQueryWithNoCommand_VulnerabilityIsReported()
    {
        Assert.Throws<InvalidOperationException>(() => new SqliteCommand().ExecuteNonQuery());        
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteNonQueryWithNotTainted_VulnerabilityIsNotReported()
    {
        new SqliteCommand(notTaintedQuery, dbConnection).ExecuteNonQuery();
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteReaderWithNotTainted_VulnerabilityIsNotReported()
    {
        new SqliteCommand(notTaintedQuery, dbConnection).ExecuteReader();
        AssertNotVulnerable();
    }

#if NET5_0_OR_GREATER
    // Not calling CreateDbCommandScope in netcore3.0, netcore3.1, netcore 2.1
    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteReaderWithTainted_VulnerabilityIsReported()
    {
        var results = new SqliteCommand(taintedQuery, dbConnection).ExecuteReader();
        AssertVulnerable();
    }


    // Not calling CreateDbCommandScope in netcore3.0, netcore3.1, netcore 2.1
    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteReaderWithTainted_VulnerabilityIsReported()
    {
        var results = new SqliteCommand(taintedQuery, dbConnection).ExecuteReader();
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteReaderCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        new SqliteCommand(taintedQuery, dbConnection).ExecuteReader(CommandBehavior.Default);
        AssertVulnerable();
    }

    // Not calling CreateDbCommandScope in netcore3.0, netcore3.1, netcore 2.1
    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteReaderCommandBehaviorWithTainted_Tainted()
    {
        var reader = new SqliteCommand(taintedQuery, dbConnection).ExecuteReader(CommandBehavior.Default);
        reader.Read();
        AssertVulnerable();
    }

    // Not calling CreateDbCommandScope in netcore3.0, netcore3.1, netcore 2.1
    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteReaderCommandBehaviorWithTainted_Tainted2()
    {
        var reader = new SqliteCommand(taintedQuery, dbConnection).ExecuteReader(CommandBehavior.Default);
        reader.Read();
        AssertVulnerable();
    }

    // Not calling CreateDbCommandScope in netcore3.0, netcore3.1, netcore 2.1
    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteReaderCommandBehaviorWithTainted_Tainted3()
    {
        var reader = new SqliteCommand(taintedQuery, dbConnection).ExecuteReader(CommandBehavior.Default);
        reader.Read();
        AssertVulnerable();
    }

    // Not calling CreateDbCommandScope in netcore3.0, netcore3.1, netcore 2.1
    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteReaderAsyncCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        new SqliteCommand(taintedQuery, dbConnection).ExecuteReaderAsync(CommandBehavior.Default);
        AssertVulnerable();
    }

    // Not calling CreateDbCommandScope in netcore3.0, netcore3.1, netcore 2.1
    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteReaderAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        new SqliteCommand(taintedQuery, dbConnection).ExecuteReaderAsync(CancellationToken.None);
        AssertVulnerable();
    }

    // Not calling CreateDbCommandScope in netcore3.0, netcore3.1, netcore 2.1
    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteReaderAsyncCancellationTokenCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        new SqliteCommand(taintedQuery, dbConnection).ExecuteReaderAsync(CommandBehavior.Default, CancellationToken.None);
        AssertVulnerable();
    }

    // Not calling CreateDbCommandScope in netcore3.0, netcore3.1, netcore 2.1
    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteReaderAsyncWithTainted_VulnerabilityIsReported()
    {
        new SqliteCommand(taintedQuery, dbConnection).ExecuteReaderAsync();
        AssertVulnerable();
    }
#endif

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteReaderAsyncCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        new SqliteCommand(taintedQuery, dbConnection).ExecuteReaderAsync(CommandBehavior.Default);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteReaderAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        new SqliteCommand(taintedQuery, dbConnection).ExecuteReaderAsync(CancellationToken.None);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteReaderAsyncCancellationTokenCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        new SqliteCommand(taintedQuery, dbConnection).ExecuteReaderAsync(CommandBehavior.Default, CancellationToken.None);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteReaderAsyncWithTainted_VulnerabilityIsReported()
    {
        new SqliteCommand(taintedQuery, dbConnection).ExecuteReaderAsync();
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteReaderCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        new SqliteCommand(notTaintedQuery, dbConnection).ExecuteReader(CommandBehavior.Default);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteScalarWithTainted_VulnerabilityIsReported()
    {
        new SqliteCommand(taintedQuery, dbConnection).ExecuteScalar();
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteScalarWithNotTainted_VulnerabilityIsNotReported()
    {
        new SqliteCommand(notTaintedQuery, dbConnection).ExecuteScalar();
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteReaderAsyncCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        new SqliteCommand(notTaintedQuery, dbConnection).ExecuteReaderAsync(CommandBehavior.Default);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteReaderAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        new SqliteCommand(notTaintedQuery, dbConnection).ExecuteReaderAsync(CancellationToken.None);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteReaderAsyncCancellationTokenCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        new SqliteCommand(notTaintedQuery, dbConnection).ExecuteReaderAsync(CommandBehavior.Default, CancellationToken.None);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteReaderAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        new SqliteCommand(notTaintedQuery, dbConnection).ExecuteReaderAsync();
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteScalarAsyncWithTainted_VulnerabilityIsReported()
    {
        new SqliteCommand(taintedQuery, dbConnection).ExecuteScalarAsync();
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteScalarAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        new SqliteCommand(notTaintedQuery, dbConnection).ExecuteScalarAsync();
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteScalarAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        new SqliteCommand(taintedQuery, dbConnection).ExecuteScalarAsync(CancellationToken.None);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteScalarAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        new SqliteCommand(notTaintedQuery, dbConnection).ExecuteScalarAsync(CancellationToken.None);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteNonQueryAsyncWithTainted_VulnerabilityIsReported()
    {
        new SqliteCommand(taintedQuery, dbConnection).ExecuteNonQueryAsync();
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteNonQueryAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        new SqliteCommand(notTaintedQuery, dbConnection).ExecuteNonQueryAsync();
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteNonQueryAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        new SqliteCommand(taintedQuery, dbConnection).ExecuteNonQueryAsync(CancellationToken.None);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftDataSqliteCommand_WhenCallingExecuteNonQueryAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        new SqliteCommand(notTaintedQuery, dbConnection).ExecuteNonQueryAsync(CancellationToken.None);
        AssertNotVulnerable();
    }
}
