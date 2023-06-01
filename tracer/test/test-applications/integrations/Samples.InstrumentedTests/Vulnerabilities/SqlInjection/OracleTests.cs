using System;
using System.Data;
using System.Reflection;
using System.Threading;
using Oracle.ManagedDataAccess.Client;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class OracleTests : InstrumentationTestsBase, IDisposable
{
    private string _connectionString;
    private OracleConnection _connection;
    protected string QueryUnsafe;
    protected string taintedValue = "tainted";
    protected string notTaintedValue = "nottainted";
    protected string CommandSafe;
    protected string CommandUnsafe;
    protected string QuerySafe;

    public OracleTests()
    {
        AddTainted(taintedValue);
        QueryUnsafe = "SELECT * from Books where Title = '" + taintedValue + "'";
        CommandUnsafe = "Update Books set Title = Title where Title ='" + taintedValue + "'";
        _connectionString = "Data Source=localhost:1521/xe;user id=DUMMY;password=DUMMY";
        _connection = new OracleConnection(_connectionString);
        CommandSafe = "Update Books set Title = Title where Title ='" + notTaintedValue + "'";
        QuerySafe = "SELECT * from Books where Title = '" + notTaintedValue + "'";
    }

    public void Dispose()
    {
    }

    // We exclude the tests that only pass when using a real MySql Connection
    // These tests have been left here for local testing purposes with MySql installed

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QueryUnsafe, _connection).ExecuteNonQuery());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported2()
    {
        TestDummyDDBBCall(() => new OracleCommand(QueryUnsafe).ExecuteNonQuery());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteNonQueryWithNull_VulnerabilityIsReported()
    {
        Assert.Throws<InvalidOperationException>(() => new OracleCommand(null).ExecuteNonQuery());
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteNonQueryWithNoCommand_VulnerabilityIsReported()
    {
        Assert.Throws<InvalidOperationException>(() => new OracleCommand().ExecuteNonQuery());
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteNonQueryWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(CommandSafe, _connection).ExecuteNonQuery());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QueryUnsafe, _connection).ExecuteReader());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QuerySafe, _connection).ExecuteReader());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QueryUnsafe, _connection).ExecuteReader(CommandBehavior.Default));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QuerySafe, _connection).ExecuteReader(CommandBehavior.Default));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteScalarWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QueryUnsafe, _connection).ExecuteScalar());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteScalarWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QuerySafe, _connection).ExecuteScalar());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderAsyncCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QueryUnsafe, _connection).ExecuteReaderAsync(CommandBehavior.Default).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderAsyncCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QuerySafe, _connection).ExecuteReaderAsync(CommandBehavior.Default).Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QueryUnsafe, _connection).ExecuteReaderAsync(CancellationToken.None).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QuerySafe, _connection).ExecuteReaderAsync(CancellationToken.None).Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderAsyncCancellationTokenCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QueryUnsafe, _connection).ExecuteReaderAsync(CommandBehavior.Default, CancellationToken.None).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderAsyncCancellationTokenCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QuerySafe, _connection).ExecuteReaderAsync(CommandBehavior.Default, CancellationToken.None).Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderAsyncWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QueryUnsafe, _connection).ExecuteReaderAsync().Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QuerySafe, _connection).ExecuteReaderAsync().Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteScalarAsyncWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QueryUnsafe, _connection).ExecuteScalarAsync().Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteScalarAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QuerySafe, _connection).ExecuteScalarAsync().Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteScalarAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QueryUnsafe, _connection).ExecuteScalarAsync(CancellationToken.None).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteScalarAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QuerySafe, _connection).ExecuteScalarAsync(CancellationToken.None).Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteNonQueryAsyncWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QueryUnsafe, _connection).ExecuteNonQueryAsync().Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteNonQueryAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QuerySafe, _connection).ExecuteNonQueryAsync().Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteNonQueryAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QueryUnsafe, _connection).ExecuteNonQueryAsync(CancellationToken.None).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteNonQueryAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(QuerySafe, _connection).ExecuteNonQueryAsync(CancellationToken.None).Wait());
        AssertNotVulnerable();
    }
}
