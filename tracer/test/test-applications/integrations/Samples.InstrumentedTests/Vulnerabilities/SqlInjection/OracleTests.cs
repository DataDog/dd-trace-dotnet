using System;
using System.Data;
using System.Threading;
using Oracle.ManagedDataAccess.Client;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class OracleTests : InstrumentationTestsBase, IDisposable
{
    private string _connectionString;
    private OracleConnection _connection;
    private string _queryUnsafe;
    private string _taintedValue = "tainted";
    private string _notTaintedValue = "nottainted";
    private string _commandSafe;
    private string _commandUnsafe;
    private string _querySafe;

    public OracleTests()
    {
        AddTainted(_taintedValue);
        _queryUnsafe = "SELECT * from Books where Title = '" + _taintedValue + "'";
        _commandUnsafe = "Update Books set Title = Title where Title ='" + _taintedValue + "'";
        _connectionString = "Data Source=localhost:1521/xe;user id=DUMMY;password=DUMMY";
        _connection = new OracleConnection(_connectionString);
        _commandSafe = "Update Books set Title = Title where Title ='" + _notTaintedValue + "'";
        _querySafe = "SELECT * from Books where Title = '" + _notTaintedValue + "'";
    }

    // We exclude the tests that only pass when using a real MySql Connection
    // These tests have been left here for local testing purposes with MySql installed

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_queryUnsafe, _connection).ExecuteNonQuery());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported2()
    {
        TestDummyDDBBCall(() => new OracleCommand(_commandUnsafe).ExecuteNonQuery());
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
        TestDummyDDBBCall(() => new OracleCommand(_commandSafe, _connection).ExecuteNonQuery());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_queryUnsafe, _connection).ExecuteReader());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_querySafe, _connection).ExecuteReader());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_queryUnsafe, _connection).ExecuteReader(CommandBehavior.Default));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_querySafe, _connection).ExecuteReader(CommandBehavior.Default));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteScalarWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_queryUnsafe, _connection).ExecuteScalar());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteScalarWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_querySafe, _connection).ExecuteScalar());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderAsyncCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_queryUnsafe, _connection).ExecuteReaderAsync(CommandBehavior.Default).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderAsyncCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_querySafe, _connection).ExecuteReaderAsync(CommandBehavior.Default).Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_queryUnsafe, _connection).ExecuteReaderAsync(CancellationToken.None).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_querySafe, _connection).ExecuteReaderAsync(CancellationToken.None).Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderAsyncCancellationTokenCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_queryUnsafe, _connection).ExecuteReaderAsync(CommandBehavior.Default, CancellationToken.None).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderAsyncCancellationTokenCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_querySafe, _connection).ExecuteReaderAsync(CommandBehavior.Default, CancellationToken.None).Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderAsyncWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_queryUnsafe, _connection).ExecuteReaderAsync().Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteReaderAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_querySafe, _connection).ExecuteReaderAsync().Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteScalarAsyncWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_queryUnsafe, _connection).ExecuteScalarAsync().Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteScalarAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_querySafe, _connection).ExecuteScalarAsync().Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteScalarAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_queryUnsafe, _connection).ExecuteScalarAsync(CancellationToken.None).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteScalarAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_querySafe, _connection).ExecuteScalarAsync(CancellationToken.None).Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteNonQueryAsyncWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_queryUnsafe, _connection).ExecuteNonQueryAsync().Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteNonQueryAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_querySafe, _connection).ExecuteNonQueryAsync().Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteNonQueryAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_queryUnsafe, _connection).ExecuteNonQueryAsync(CancellationToken.None).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAOracleCommand_WhenCallingExecuteNonQueryAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new OracleCommand(_querySafe, _connection).ExecuteNonQueryAsync(CancellationToken.None).Wait());
        AssertNotVulnerable();
    }
}
