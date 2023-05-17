using System.Data;
using System.Data.Common;
using System.Threading;
using MySql.Data.MySqlClient;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class MySqlTests : DatabaseInstrumentationTestsBase
{
    string _connectionString;
    public MySqlTests()
    {
        
    }

    protected override string GetConnectionString()
    {
        return _connectionString;
    }

    protected override DbConnection GetDbConnection(string connectionString)
    {
        return new MySqlConnection(_connectionString);
    }

    protected override DbCommand GetCommand(string query, DbConnection connection)
    {
        return new MySqlCommand(query, (MySqlConnection)connection);
    }

    protected override DbCommand GetCommand(string query)
    {
        return new MySqlCommand(query);
    }

    protected override DbCommand GetCommand()
    {
        return new MySqlCommand();
    }

    private MySqlConnection OpenMysqlConnection()
    {
        return (MySqlConnection)OpenConnection();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteDataRowWithTainted_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteDataRow(connectionString, QueryUnsafe, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteDataRowWithTainted2Params_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteDataRow(connectionString, QueryUnsafe);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteReaderWithTaintedDatabaseConnection_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteReader(databaseConnection, QueryUnsafe);
            AssertVulnerable();
        }
    }
    
    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteReaderWithTainted_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteReader(connectionString, QueryUnsafe);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteReaderWithTainted3Params_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteReader(connectionString, QueryUnsafe, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteReaderWithTaintedDatabaseConnection3Params_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteReader(databaseConnection, QueryUnsafe, null);
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteReaderAsyncWithTainted_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteReaderAsync(connectionString, QueryUnsafe);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteReaderAsyncWithTainted3Params_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteReaderAsync(connectionString, QueryUnsafe, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteReaderAsyncWithTaintedDatabaseConnection2Params_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteReaderAsync(databaseConnection, QueryUnsafe).Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteReaderAsyncWithTaintedDatabaseConnection3Params_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteReaderAsync(databaseConnection, QueryUnsafe, null).Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteReaderAsyncWithTainted3ParamsCancellation_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteReaderAsync(connectionString, QueryUnsafe, CancellationToken.None);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteReaderAsyncWithTainted4Params_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteReaderAsync(connectionString, QueryUnsafe, CancellationToken.None, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteReaderAsyncWithTaintedDatabaseConnection3ParamsCancellation_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteReaderAsync(databaseConnection, QueryUnsafe, CancellationToken.None).Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteReaderAsyncWithTaintedDatabaseConnection4Params_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteReaderAsync(databaseConnection, QueryUnsafe, CancellationToken.None, null).Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteScalarWithTainted_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteScalar(connectionString, CommandUnsafe);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteScalarWithTaintedDatabaseConnection2Params_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteScalar(databaseConnection, CommandUnsafe);
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteScalarWithTainted3Params_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteScalar(connectionString, CommandUnsafe, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteScalarWithTaintedDatabaseConnection3Params_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteScalar(databaseConnection, CommandUnsafe, null);
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteScalarAsyncWithTainted_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteScalarAsync(connectionString, CommandUnsafe);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteScalarAsyncWithTaintedDatabaseConnection2Params_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteScalarAsync(databaseConnection, CommandUnsafe).Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteScalarAsyncWithTainted3Params_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteScalarAsync(connectionString, CommandUnsafe, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteScalarAsyncWithTaintedDatabaseConnection3Params_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteScalarAsync(databaseConnection, CommandUnsafe, null).Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteScalarAsyncWithTaintedParamsCancellation_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteScalarAsync(connectionString, CommandUnsafe, CancellationToken.None);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteScalarAsyncWithTaintedDatabaseConnection3ParamsCancellation_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteScalarAsync(databaseConnection, CommandUnsafe, CancellationToken.None).Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteScalarAsyncWithTainted4Params_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteScalarAsync(connectionString, CommandUnsafe, CancellationToken.None, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteScalarAsyncWithTaintedDatabaseConnection4Params_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteScalarAsync(databaseConnection, CommandUnsafe, CancellationToken.None, null).Wait();
            AssertVulnerable();
        }
    }
    
    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteDatasetWithTainted_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteDataset(connectionString, QueryUnsafe);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteDatasetWithTainted3Params_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteDataset(connectionString, QueryUnsafe, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteDatasetWithTaintedDatabaseConnection2Params_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteDataset(databaseConnection, QueryUnsafe);
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteDatasetWithTaintedDatabaseConnection3Params_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteDataset(databaseConnection, QueryUnsafe, null);
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteDatasetAsyncWithTainted_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteDatasetAsync(connectionString, QueryUnsafe).Wait();
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteDatasetAsyncWithTainted3Params_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteDatasetAsync(connectionString, QueryUnsafe, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteDatasetAsyncWithTaintedDatabaseConnection2Params_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteDatasetAsync(databaseConnection, QueryUnsafe).Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteDatasetAsyncWithTaintedDatabaseConnection3Params_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteDatasetAsync(databaseConnection, QueryUnsafe, null).Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteDatasetAsyncWithTainted3ParamsCancellation_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteDatasetAsync(connectionString, QueryUnsafe, CancellationToken.None);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteDatasetAsyncWithTainted4Params_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteDatasetAsync(connectionString, QueryUnsafe, CancellationToken.None, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteDatasetAsyncWithTaintedDatabaseConnection3ParamsCancellation_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteDatasetAsync(databaseConnection, QueryUnsafe, CancellationToken.None).Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteDatasetAsyncWithTaintedDatabaseConnection4Params_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteDatasetAsync(databaseConnection, QueryUnsafe, CancellationToken.None, null).Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingoWithTainted_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteNonQuery(connectionString, CommandUnsafe);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingoWithTaintedDatabaseConnection2Params_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteNonQuery(databaseConnection, CommandUnsafe);
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteNonQuery(connectionString, CommandUnsafe, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteNonQueryWithTaintedDatabaseConnection3Params_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteNonQuery(databaseConnection, CommandUnsafe, null);
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteNonQueryAsyncWithTainted_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteNonQueryAsync(connectionString, CommandUnsafe);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteNonQueryAsyncWithTaintedDatabaseConnection2Params_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteNonQueryAsync(databaseConnection, CommandUnsafe).Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteNonQueryAsyncWithTainted3Params_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteNonQueryAsync(connectionString, CommandUnsafe, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteNonQueryAsyncWithTaintedDatabaseConnection3Params_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteNonQueryAsync(databaseConnection, CommandUnsafe, null).Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteNonQueryAsyncWithTainted3ParamsCancellation_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteNonQueryAsync(connectionString, CommandUnsafe, CancellationToken.None);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteNonQueryAsyncWithTaintedDatabaseConnection3ParamsCancellation_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteNonQueryAsync(databaseConnection, CommandUnsafe, CancellationToken.None).Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteNonQueryAsyncWithTainted4Params_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteNonQueryAsync(connectionString, CommandUnsafe, CancellationToken.None, null);
        AssertVulnerable();
    }

    
    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteNonQueryAsyncWithTaintedDatabaseConnection4Params_VulnerabilityIsReported()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            MySqlHelper.ExecuteNonQueryAsync(databaseConnection, CommandUnsafe, CancellationToken.None, null).Wait();
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteDataRowAsyncWithTainted_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteDataRowAsync(connectionString, QueryUnsafe);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteDataRowAsyncWithTaintedDatabaseConnection3Params_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteDataRowAsync(connectionString, QueryUnsafe, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteDataRowAsyncWithTainted4ParamsCancellation_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteDataRowAsync(connectionString, QueryUnsafe, CancellationToken.None);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlHelper_WhenCallingExecuteDataRowAsyncWithTainted4Params_VulnerabilityIsReported()
    {
        MySqlHelper.ExecuteDataRowAsync(connectionString, QueryUnsafe, CancellationToken.None, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteReaderCommandBehaviorWithTainted_Tainted2()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            var reader = new MySqlCommand(QueryUnsafe, databaseConnection).ExecuteReader(CommandBehavior.Default);
            reader.Read();
            AssertTainted(reader["Title"]);
        }
    }

    [Fact]
    public void GivenAMySqlCommand_WhenCallingExecuteReaderCommandBehaviorWithTainted_Tainted4()
    {
        using (var databaseConnection = OpenMysqlConnection())
        {
            var reader = new MySqlCommand(QueryUnsafe, databaseConnection).ExecuteReader(CommandBehavior.Default);
            reader.Read();
            AssertTainted(reader.GetString("Title"));
        }
    }
}
