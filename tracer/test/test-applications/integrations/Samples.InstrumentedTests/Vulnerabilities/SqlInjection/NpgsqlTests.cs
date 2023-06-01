using System;
using System.Data;
using System.Threading;
using Npgsql;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class NpgsqlTests : InstrumentationTestsBase, IDisposable
{
    private string _connectionString;
    private NpgsqlConnection _connection;
    protected string QueryUnsafe;
    protected string taintedValue = "tainted";
    protected string notTaintedValue = "nottainted";
    protected string CommandSafe;
    protected string CommandUnsafe;
    protected string QuerySafe;

    public NpgsqlTests()
    {
        AddTainted(taintedValue);
        QueryUnsafe = "SELECT * from Books where Title = '" + taintedValue + "'";
        CommandUnsafe = "Update Books set Title = Title where Title ='" + taintedValue + "'";
        _connectionString = "Host=localhost;Port=5432;Username=DUMMY;password=DUMMY";
        _connection = new NpgsqlConnection(_connectionString);
        CommandSafe = "Update Books set Title = Title where Title ='" + notTaintedValue + "'";
        QuerySafe = "SELECT * from Books where Title = '" + notTaintedValue + "'";
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
        _connection = null;
    }

    // We exclude the tests that only pass when using a real MySql Connection
    // These tests have been left here for local testing purposes with MySql installed

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QueryUnsafe, _connection).ExecuteNonQuery());
        AssertVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported2()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QueryUnsafe, _connection, null).ExecuteNonQuery());
        AssertVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported3()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QueryUnsafe).ExecuteNonQuery());
        AssertVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteNonQueryWithNull_VulnerabilityIsReported()
    {
        Assert.Throws<InvalidOperationException>(() => new NpgsqlCommand(null).ExecuteNonQuery());
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteNonQueryWithNoCommand_VulnerabilityIsReported()
    {
        Assert.Throws<InvalidOperationException>(() => new NpgsqlCommand().ExecuteNonQuery());
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteNonQueryWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(CommandSafe, _connection).ExecuteNonQuery());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteReaderWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QueryUnsafe, _connection).ExecuteReader());
        AssertVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteReaderWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QuerySafe, _connection).ExecuteReader());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteReaderCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QueryUnsafe, _connection).ExecuteReader(CommandBehavior.Default));
        AssertVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteReaderCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QuerySafe, _connection).ExecuteReader(CommandBehavior.Default));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteScalarWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QueryUnsafe, _connection).ExecuteScalar());
        AssertVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteScalarWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QuerySafe, _connection).ExecuteScalar());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteReaderAsyncCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QueryUnsafe, _connection).ExecuteReaderAsync(CommandBehavior.Default).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteReaderAsyncCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QuerySafe, _connection).ExecuteReaderAsync(CommandBehavior.Default).Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteReaderAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QueryUnsafe, _connection).ExecuteReaderAsync(CancellationToken.None).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteReaderAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QuerySafe, _connection).ExecuteReaderAsync(CancellationToken.None).Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteReaderAsyncCancellationTokenCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QueryUnsafe, _connection).ExecuteReaderAsync(CommandBehavior.Default, CancellationToken.None).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteReaderAsyncCancellationTokenCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QuerySafe, _connection).ExecuteReaderAsync(CommandBehavior.Default, CancellationToken.None).Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteReaderAsyncWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QueryUnsafe, _connection).ExecuteReaderAsync().Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteReaderAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QuerySafe, _connection).ExecuteReaderAsync().Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteScalarAsyncWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QueryUnsafe, _connection).ExecuteScalarAsync().Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteScalarAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QuerySafe, _connection).ExecuteScalarAsync().Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteScalarAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QueryUnsafe, _connection).ExecuteScalarAsync(CancellationToken.None).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteScalarAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QuerySafe, _connection).ExecuteScalarAsync(CancellationToken.None).Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteNonQueryAsyncWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QueryUnsafe, _connection).ExecuteNonQueryAsync().Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteNonQueryAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QuerySafe, _connection).ExecuteNonQueryAsync().Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteNonQueryAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QueryUnsafe, _connection).ExecuteNonQueryAsync(CancellationToken.None).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenANpgsqlCommand_WhenCallingExecuteNonQueryAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new NpgsqlCommand(QuerySafe, _connection).ExecuteNonQueryAsync(CancellationToken.None).Wait());
        AssertNotVulnerable();
    }
}
