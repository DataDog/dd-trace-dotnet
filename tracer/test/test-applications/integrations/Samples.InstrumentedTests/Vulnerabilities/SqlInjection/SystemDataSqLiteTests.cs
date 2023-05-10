using System;
using System.Data;
using System.Data.SQLite;
using System.Threading;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class SystemDataSqLiteTests : InstrumentationTestsBase, IDisposable
{
    protected static string ScalarCommandUnsafe;
    protected static string taintedValue = "Name1";
    static SQLiteConnection dbConnection;
    protected string notTaintedValue = "nottainted";
    string taintedQuery;
    string notTaintedQuery;

    public void Dispose()
    {
        dbConnection.Close();
        dbConnection.Dispose();
        dbConnection = null;
    }

    public SystemDataSqLiteTests()
    {
        dbConnection = SqliteDDBBCreator.CreateDatabase();
        AddTainted(taintedValue);
        taintedQuery = "SELECT * from Persons where name = '" + taintedValue + "'";
        notTaintedQuery = "SELECT * from Persons where name = 'Emilio'";
        ScalarCommandUnsafe = "SELECT Count(*) from Persons where Name like '" + taintedValue + "'";
    }

    [Fact]
    // [ExpectedException(typeof(InvalidOperationException))]

    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported1()
    {
        new SQLiteCommand(taintedQuery).ExecuteNonQuery();
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
        AssertTainted(reader[1]);
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteReaderCommandBehaviorWithTainted_Tainted2()
    {
        var reader = new SQLiteCommand(taintedQuery, dbConnection).ExecuteReader(CommandBehavior.Default);
        reader.Read();
        AssertTainted(reader["Name"]);
    }

#if NETCOREAPP3_1_OR_GREATER
    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteReaderCommandBehaviorWithTainted_Tainted21()
    {
        var reader = new SQLiteCommand(taintedQuery, dbConnection).ExecuteReader(CommandBehavior.Default);
        reader.Read();
        AssertTainted(reader.GetString("Name"));
    }
#endif
    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteReaderCommandBehaviorWithTainted_Tainted3()
    {
        var reader = new SQLiteCommand(taintedQuery, dbConnection).ExecuteReader(CommandBehavior.Default);
        reader.Read();
        AssertTainted(reader.GetValue(1));
    }

    [Fact]
    public void GivenASystemDataSQLiteCommand_WhenCallingExecuteReaderCommandBehaviorWithTainted_Tainted4()
    {
        var reader = new SQLiteCommand(taintedQuery, dbConnection).ExecuteReader(CommandBehavior.Default);
        reader.Read();
        AssertTainted(reader.GetString(1));
    }
}
