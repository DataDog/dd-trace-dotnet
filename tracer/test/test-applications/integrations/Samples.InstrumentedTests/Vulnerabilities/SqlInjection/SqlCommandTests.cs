using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using FluentAssertions;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class SqlCommandTests : InstrumentationTestsBase, IDisposable
{
    protected string taintedValue = "tainted";
    protected string notTaintedValue = "nottainted";
    protected string allPersonsQuery = "SELECT * from Persons";
    string taintedQuery;
    string notTaintedQuery;

    SqlConnection databaseConnection;

    public SqlCommandTests()
    {
        databaseConnection = SqlDDBBCreator.Create();
        AddTainted(taintedValue);
        taintedQuery = "SELECT * from persons where name = '" + taintedValue + "'";
        notTaintedQuery = "SELECT * from persons where name = 'Emilio'";
    }

    public override void Dispose()
    {
        if (databaseConnection != null)
        {
            databaseConnection.Close();
            databaseConnection.Dispose();
            databaseConnection = null;
        }
        base.Dispose();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported()
    {
        TestRealDDBBLocalCall(() => new SqlCommand(taintedQuery, databaseConnection).ExecuteNonQuery());
        AssertVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported2()
    {
        TestRealDDBBLocalCall(() => new SqlCommand(taintedQuery, databaseConnection, null).ExecuteNonQuery());
        AssertVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteNonQueryWithNull_VulnerabilityIsReported()
    {
        Assert.Throws<InvalidOperationException>(() => new SqlCommand(null, databaseConnection).ExecuteNonQuery());
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteNonQueryWithNoCommand_VulnerabilityIsReported()
    {
        Assert.Throws<InvalidOperationException>(() => new SqlCommand().ExecuteNonQuery());
    }

    [Fact]
    public void GivenADbCommand_WhenCallingExecuteNonQueryWithNull_VulnerabilityIsReported()
    {
        Assert.Throws<InvalidOperationException>(() => new SqlCommand(null).ExecuteNonQuery());
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteNonQueryWithNotTainted_VulnerabilityIsNotReported()
    {
        TestRealDDBBLocalCall(() => new SqlCommand(notTaintedQuery, databaseConnection).ExecuteNonQuery());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteReaderWithTainted_VulnerabilityIsReported()
    {
        using var reader = TestRealDDBBLocalCall(() => new SqlCommand(taintedQuery, databaseConnection).ExecuteReader());
        AssertVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteReaderWithNotTainted_VulnerabilityIsNotReported()
    {
        using var reader = TestRealDDBBLocalCall(() => new SqlCommand(notTaintedQuery, databaseConnection).ExecuteReader());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteReaderCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        using var reader = TestRealDDBBLocalCall(() => new SqlCommand(taintedQuery, databaseConnection).ExecuteReader(CommandBehavior.Default));
        AssertVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteReaderCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        using var reader = TestRealDDBBLocalCall(() => new SqlCommand(notTaintedQuery, databaseConnection).ExecuteReader(CommandBehavior.Default));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteScalarWithTainted_VulnerabilityIsReported()
    {
        TestRealDDBBLocalCall(() => new SqlCommand(taintedQuery, databaseConnection).ExecuteScalar());
        AssertVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteScalarWithNotTainted_VulnerabilityIsNotReported()
    {
        TestRealDDBBLocalCall(() => new SqlCommand(notTaintedQuery, databaseConnection).ExecuteScalar());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteReaderAsyncCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        using var reader = TestRealDDBBLocalCall(() => new SqlCommand(taintedQuery, databaseConnection).ExecuteReaderAsync(CommandBehavior.Default)).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteReaderAsyncCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        using var reader = TestRealDDBBLocalCall(() => new SqlCommand(notTaintedQuery, databaseConnection).ExecuteReaderAsync(CommandBehavior.Default)).Result;
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteReaderAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        using var reader = TestRealDDBBLocalCall(() => new SqlCommand(taintedQuery, databaseConnection).ExecuteReaderAsync(CancellationToken.None)).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteReaderAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        using var reader = TestRealDDBBLocalCall(() => new SqlCommand(notTaintedQuery, databaseConnection).ExecuteReaderAsync(CancellationToken.None)).Result;
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteReaderAsyncCancellationTokenCommandBehaviorWithTainted_VulnerabilityIsReported()
    {
        using var reader = TestRealDDBBLocalCall(() => new SqlCommand(taintedQuery, databaseConnection).ExecuteReaderAsync(CommandBehavior.Default, CancellationToken.None)).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteReaderAsyncCancellationTokenCommandBehaviorWithNotTainted_VulnerabilityIsNotReported()
    {
        using var reader = TestRealDDBBLocalCall(() => new SqlCommand(notTaintedQuery, databaseConnection).ExecuteReaderAsync(CommandBehavior.Default, CancellationToken.None)).Result;
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteReaderAsyncWithTainted_VulnerabilityIsReported()
    {
        using var reader = TestRealDDBBLocalCall(() => new SqlCommand(taintedQuery, databaseConnection).ExecuteReaderAsync()).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteReaderAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        using var reader = TestRealDDBBLocalCall(() => new SqlCommand(notTaintedQuery, databaseConnection).ExecuteReaderAsync()).Result;
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteScalarAsyncWithTainted_VulnerabilityIsReported()
    {
        TestRealDDBBLocalCall(() => new SqlCommand(taintedQuery, databaseConnection).ExecuteScalarAsync());
        AssertVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteScalarAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        TestRealDDBBLocalCall(() => new SqlCommand(notTaintedQuery, databaseConnection).ExecuteScalarAsync());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteScalarAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        TestRealDDBBLocalCall(() => new SqlCommand(taintedQuery, databaseConnection).ExecuteScalarAsync(CancellationToken.None));
        AssertVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteScalarAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        TestRealDDBBLocalCall(() => new SqlCommand(notTaintedQuery, databaseConnection).ExecuteScalarAsync(CancellationToken.None));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteNonQueryAsyncWithTainted_VulnerabilityIsReported()
    {
        TestRealDDBBLocalCall(() => new SqlCommand(taintedQuery, databaseConnection).ExecuteNonQueryAsync());
        AssertVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteNonQueryAsyncWithNotTainted_VulnerabilityIsNotReported()
    {
        TestRealDDBBLocalCall(() => new SqlCommand(notTaintedQuery, databaseConnection).ExecuteNonQueryAsync());
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteNonQueryAsyncCancellationTokenWithTainted_VulnerabilityIsReported()
    {
        TestRealDDBBLocalCall(() => new SqlCommand(taintedQuery, databaseConnection).ExecuteNonQueryAsync(CancellationToken.None));
        AssertVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteNonQueryAsyncCancellationTokenWithNotTainted_VulnerabilityIsNotReported()
    {
        TestRealDDBBLocalCall(() => new SqlCommand(notTaintedQuery, databaseConnection).ExecuteNonQueryAsync(CancellationToken.None));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASqlReader_WhenCallingGetString_OutputIsTainted()
    {
        using (var reader = TestRealDDBBLocalCall(() => new SqlCommand(allPersonsQuery, databaseConnection).ExecuteReader()))
        {
            reader.Read().Should().BeTrue();
            {
                for (int x = 0; x < reader.FieldCount; x++)
                {
                    if (reader.GetFieldType(x) == typeof(string) && !reader.IsDBNull(x))
                    {
                        var value = reader.GetString(x);
                        AssertTainted(value, $"Reader type : {reader.GetType().FullName} Assembly: {reader.GetType().Assembly.FullName}");
                    }
                }
            }

            reader.Read().Should().BeTrue();
            {
                for (int x = 0; x < reader.FieldCount; x++)
                {
                    if (reader.GetFieldType(x) == typeof(string) && !reader.IsDBNull(x))
                    {
                        var value = reader.GetString(x);
                        AssertNotTainted(value, $"Reader type : {reader.GetType().FullName} Assembly: {reader.GetType().Assembly.FullName}");
                    }
                }
            }
        }
    }
}
