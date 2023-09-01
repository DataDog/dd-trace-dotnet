using System;
using System.Data;
using System.Threading;
using MySql.Data.MySqlClient;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class MySqlTests : InstrumentationTestsBase, IDisposable
{
    private string _connectionString;
    private MySqlConnection _connection;
    private string _queryUnsafe;
    private string _taintedValue = "tainted";
    private string _notTaintedValue = "nottainted";
    private string _commandSafe;
    private string _commandUnsafe;
    private string _querySafe;

    public MySqlTests()
    {
        AddTainted(_taintedValue);
        _queryUnsafe = "SELECT * from Books where Title = '" + _taintedValue + "'";
        _commandUnsafe = "Update Books set Title = Title where Title ='" + _taintedValue + "'";
        _connection = MySqlDDBBCreator.Create();
        _connectionString = MySqlDDBBCreator.connectionString;
        _commandSafe = "Update Books set Title = Title where Title ='" + _notTaintedValue + "'";
        _querySafe = "SELECT * from Books where Title = '" + _notTaintedValue + "'";
    }

    public override void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
        _connection = null;
        base.Dispose();
    }

    // We exclude the tests that only pass when using a real MySql Connection
    // These tests have been left here for local testing purposes with MySql installed

    [Fact(Skip = "Test only with a real mySQl DDBB")]
    public void GivenAMySqlHelper_WhenCallingExecuteDataRowWithTainted_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteDataRow(_connectionString, _queryUnsafe, null);
        AssertVulnerable();
    }

    [Fact(Skip = "Test only with a real mySQl DDBB")]
    public void GivenAMySqlHelper_WhenCallingExecuteDataRowWithTainted2Params_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteDataRow(_connectionString, _queryUnsafe);
        AssertVulnerable();
    }

    [Fact(Skip = "Test only with a real mySQl DDBB")]
    public void GivenAMySqlHelper_WhenCallingExecuteDatasetWithTainted_connection2Params_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteDataset(_connection, _queryUnsafe);
        AssertVulnerable();
    }

    [Fact(Skip = "Test only with a real mySQl DDBB")]
    public void GivenAMySqlHelper_WhenCallingExecuteDatasetWithTainted_connection3Params_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteDataset(_connection, _queryUnsafe, null);
        AssertVulnerable();
    }

    [Fact(Skip = "Test only with a real mySQl DDBB")]
    public void GivenAMySqlHelper_WhenCallingExecuteDatasetAsyncWithTainted_connection2Params_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteDatasetAsync(_connection, _queryUnsafe).Wait();
        AssertVulnerable();
    }

    [Fact(Skip = "Test only with a real mySQl DDBB")]
    public void GivenAMySqlHelper_WhenCallingExecuteDatasetAsyncWithTainted_connection3Params_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteDatasetAsync(_connection, _queryUnsafe, null).Wait();
        AssertVulnerable();
    }

    [Fact(Skip = "Test only with a real mySQl DDBB")]
    public void GivenAMySqlHelper_WhenCallingExecuteDatasetAsyncWithTainted_connection3ParamsCancellation_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteDatasetAsync(_connection, _queryUnsafe, CancellationToken.None).Wait();
        AssertVulnerable();
    }

    [Fact(Skip = "Test only with a real mySQl DDBB")]
    public void GivenAMySqlHelper_WhenCallingExecuteDatasetAsyncWithTainted_connection4Params_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteDatasetAsync(_connection, _queryUnsafe, CancellationToken.None, null).Wait();
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteReaderWithTainted_connection_VulnerabilityIsReported()
    {
        TestDummyDDBBCall( ()=> MySqlHelper.ExecuteReader(_connection, _queryUnsafe));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteReaderWithTainted_connection3Params_VulnerabilityIsReported()
    {
        TestDummyDDBBCall( ()=> MySqlHelper.ExecuteReader(_connection, _queryUnsafe, null));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteReaderAsyncWithTainted_connection2Params_VulnerabilityIsReported()
    {
        TestDummyDDBBCall( ()=> MySqlHelper.ExecuteReaderAsync(_connection, _queryUnsafe).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteReaderAsyncWithTainted_connection3Params_VulnerabilityIsReported()
    {
        TestDummyDDBBCall( ()=> MySqlHelper.ExecuteReaderAsync(_connection, _queryUnsafe, null).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteReaderAsyncWithTainted_connection3ParamsCancellation_VulnerabilityIsReported()
    {
        TestDummyDDBBCall( ()=> MySqlHelper.ExecuteReaderAsync(_connection, _queryUnsafe, CancellationToken.None).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteReaderAsyncWithTainted_connection4Params_VulnerabilityIsReported()
    {
        TestDummyDDBBCall( ()=> MySqlHelper.ExecuteReaderAsync(_connection, _queryUnsafe, CancellationToken.None, null).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteScalarWithTainted_connection2Params_VulnerabilityIsReported()
    {
        TestDummyDDBBCall( ()=> MySqlHelper.ExecuteScalar(_connection, _commandUnsafe));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteScalarWithTainted_connection3Params_VulnerabilityIsReported()
    {
        TestDummyDDBBCall( ()=> MySqlHelper.ExecuteScalar(_connection, _commandUnsafe, null));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteScalarAsyncWithTainted_connection2Params_VulnerabilityIsReported()
    {
        TestDummyDDBBCall( ()=> MySqlHelper.ExecuteScalarAsync(_connection, _commandUnsafe).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteScalarAsyncWithTainted_connection3Params_VulnerabilityIsReported()
    {
        TestDummyDDBBCall( ()=> MySqlHelper.ExecuteScalarAsync(_connection, _commandUnsafe, null).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteScalarAsyncWithTainted_connection3ParamsCancellation_VulnerabilityIsReported()
    {
        TestDummyDDBBCall( ()=> MySqlHelper.ExecuteScalarAsync(_connection, _commandUnsafe, CancellationToken.None).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteScalarAsyncWithTainted_connection4Params_VulnerabilityIsReported()
    {
        TestDummyDDBBCall( ()=> MySqlHelper.ExecuteScalarAsync(_connection, _commandUnsafe, CancellationToken.None, null).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingoWithTainted_connection2Params_VulnerabilityIsReported()
    {
        TestDummyDDBBCall( ()=> MySqlHelper.ExecuteNonQuery(_connection, _commandUnsafe));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteNonQueryWithTainted_connection3Params_VulnerabilityIsReported()
    {
        TestDummyDDBBCall( ()=> MySqlHelper.ExecuteNonQuery(_connection, _commandUnsafe, null));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteNonQueryAsyncWithTainted_connection2Params_VulnerabilityIsReported()
    {
        TestDummyDDBBCall( ()=> MySqlHelper.ExecuteNonQueryAsync(_connection, _commandUnsafe).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteNonQueryAsyncWithTainted_connection3Params_VulnerabilityIsReported()
    {
        TestDummyDDBBCall( ()=> MySqlHelper.ExecuteNonQueryAsync(_connection, _commandUnsafe, null).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteNonQueryAsyncWithTainted_connection3ParamsCancellation_VulnerabilityIsReported()
    {
        TestDummyDDBBCall( ()=> MySqlHelper.ExecuteNonQueryAsync(_connection, _commandUnsafe, CancellationToken.None).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteNonQueryAsyncWithTainted_connection4Params_VulnerabilityIsReported()
    {
        TestDummyDDBBCall( ()=> MySqlHelper.ExecuteNonQueryAsync(_connection, _commandUnsafe, CancellationToken.None, null).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_commandUnsafe, _connection).ExecuteNonQuery());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteNonQueryWithNull_VulnerabilityIsReported()
    {
        Assert.Throws<InvalidOperationException>(() => new MySqlCommand(null).ExecuteNonQuery());
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteNonQueryWithNoCommand_VulnerabilityIsReported()
    {
        Assert.Throws<InvalidOperationException>(() => new MySqlCommand().ExecuteNonQuery());
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteNonQueryWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_commandSafe, _connection).ExecuteNonQuery());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteReaderWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_queryUnsafe, _connection).ExecuteReader());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteReaderWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_querySafe, _connection).ExecuteReader());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteReaderCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_queryUnsafe, _connection).ExecuteReader(CommandBehavior.Default));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteReaderCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_querySafe, _connection).ExecuteReader(CommandBehavior.Default));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteScalarWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_queryUnsafe, _connection).ExecuteScalar());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteScalarWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_querySafe, _connection).ExecuteScalar());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteReaderAsyncCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_queryUnsafe, _connection).ExecuteReaderAsync(CommandBehavior.Default).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteReaderAsyncCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_querySafe, _connection).ExecuteReaderAsync(CommandBehavior.Default).Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteReaderAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_queryUnsafe, _connection).ExecuteReaderAsync(CancellationToken.None).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteReaderAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_querySafe, _connection).ExecuteReaderAsync(CancellationToken.None).Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteReaderAsyncCancellationTokenCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_queryUnsafe, _connection).ExecuteReaderAsync(CommandBehavior.Default, CancellationToken.None).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteReaderAsyncCancellationTokenCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_querySafe, _connection).ExecuteReaderAsync(CommandBehavior.Default, CancellationToken.None).Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteReaderAsyncWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_queryUnsafe, _connection).ExecuteReaderAsync().Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteReaderAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_querySafe, _connection).ExecuteReaderAsync().Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteScalarAsyncWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_queryUnsafe, _connection).ExecuteScalarAsync().Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteScalarAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_querySafe, _connection).ExecuteScalarAsync().Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteScalarAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_queryUnsafe, _connection).ExecuteScalarAsync(CancellationToken.None).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteScalarAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_querySafe, _connection).ExecuteScalarAsync(CancellationToken.None).Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteNonQueryAsyncWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_queryUnsafe, _connection).ExecuteNonQueryAsync().Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteNonQueryAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_querySafe, _connection).ExecuteNonQueryAsync().Wait());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteNonQueryAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_queryUnsafe, _connection).ExecuteNonQueryAsync(CancellationToken.None).Wait());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteNonQueryAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        TestDummyDDBBCall(() => new MySqlCommand(_querySafe, _connection).ExecuteNonQueryAsync(CancellationToken.None).Wait());
        AssertNotVulnerable();
    }
}
