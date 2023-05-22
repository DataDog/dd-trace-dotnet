using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public abstract class DatabaseInstrumentationTestsBase : InstrumentationTestsBase
{
    protected string taintedValue = "tainted";
    protected string notTaintedValue = "nottainted";
    protected string connectionString;
    protected string QueryUnsafe;
    protected string QuerySafe;
    protected string CommandUnsafe;
    protected string CommandSafe;

    public DatabaseInstrumentationTestsBase()
    {
        connectionString = GetConnectionString();
        AddTainted(taintedValue);
        QueryUnsafe = "SELECT * from Books where Title = '" + taintedValue + "'";
        QuerySafe = "SELECT * from Books where Title = '" + notTaintedValue + "'";
        CommandUnsafe = "Update Books set Title = Title where Title ='" + taintedValue + "'";
        CommandSafe = "Update Books set Title = Title where Title ='" + notTaintedValue + "'";
    }

    protected DbConnection OpenConnection()
    {
        var connection = GetDbConnection(connectionString);
        return connection;
    }

    protected abstract string GetConnectionString();

    protected abstract DbConnection GetDbConnection(string connectionString);

    protected abstract DbCommand GetCommand(string query, DbConnection connection);

    protected abstract DbCommand GetCommand(string query);

    protected abstract DbCommand GetCommand();

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QueryUnsafe, databaseConnection).ExecuteNonQuery();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteNonQueryWithNull_VulnerabilityIsReported()
    {
        Assert.Throws<InvalidOperationException>(() => GetCommand(null).ExecuteNonQuery());
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteNonQueryWithNoCommand_VulnerabilityIsReported()
    {
        Assert.Throws<InvalidOperationException>(() => GetCommand().ExecuteNonQuery());
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteNonQueryWithNotTainted_VulnerabilityIsNotReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(CommandSafe, databaseConnection).ExecuteNonQuery();
            AssertNotVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteReaderWithTainted_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QueryUnsafe, databaseConnection).ExecuteReader();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteReaderWithNotTainted_VulnerabilityIsNotReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QuerySafe, databaseConnection).ExecuteReader();
            AssertNotVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteReaderCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QueryUnsafe, databaseConnection).ExecuteReader(CommandBehavior.Default);
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteReaderCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QuerySafe, databaseConnection).ExecuteReader(CommandBehavior.Default);
            AssertNotVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteScalarWithTainted_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QueryUnsafe, databaseConnection).ExecuteScalar();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteScalarWithNotTainted_VulnerabilityIsNotReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QuerySafe, databaseConnection).ExecuteScalar();
            AssertNotVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteReaderAsyncCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QueryUnsafe, databaseConnection).ExecuteReaderAsync(CommandBehavior.Default).Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteReaderAsyncCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QuerySafe, databaseConnection).ExecuteReaderAsync(CommandBehavior.Default).Wait();
            AssertNotVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteReaderAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QueryUnsafe, databaseConnection).ExecuteReaderAsync(CancellationToken.None).Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteReaderAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QuerySafe, databaseConnection).ExecuteReaderAsync(CancellationToken.None).Wait();
            AssertNotVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteReaderAsyncCancellationTokenCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QueryUnsafe, databaseConnection).ExecuteReaderAsync(CommandBehavior.Default, CancellationToken.None).Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteReaderAsyncCancellationTokenCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QuerySafe, databaseConnection).ExecuteReaderAsync(CommandBehavior.Default, CancellationToken.None).Wait();
            AssertNotVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteReaderAsyncWithTainted_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QueryUnsafe, databaseConnection).ExecuteReaderAsync().Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteReaderAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QuerySafe, databaseConnection).ExecuteReaderAsync().Wait();
            AssertNotVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteScalarAsyncWithTainted_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QueryUnsafe, databaseConnection).ExecuteScalarAsync().Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteScalarAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QuerySafe, databaseConnection).ExecuteScalarAsync().Wait();
            AssertNotVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteScalarAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QueryUnsafe, databaseConnection).ExecuteScalarAsync(CancellationToken.None).Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteScalarAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QuerySafe, databaseConnection).ExecuteScalarAsync(CancellationToken.None).Wait();
            AssertNotVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteNonQueryAsyncWithTainted_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QueryUnsafe, databaseConnection).ExecuteNonQueryAsync().Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteNonQueryAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QuerySafe, databaseConnection).ExecuteNonQueryAsync().Wait();
            AssertNotVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteNonQueryAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QueryUnsafe, databaseConnection).ExecuteNonQueryAsync(CancellationToken.None).Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteNonQueryAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        using (var databaseConnection = OpenConnection())
        {
            GetCommand(QuerySafe, databaseConnection).ExecuteNonQueryAsync(CancellationToken.None).Wait();
            AssertNotVulnerable();
        }
    }
}
