using System.Data;
using Xunit;
using Dapper;
using System;
using System.Threading;
using System.Data.Common;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

[Trait("Category", "ArmUnsupported")]
public class DapperTests : InstrumentationTestsBase, IDisposable
{
    protected string taintedValue = "tainted";
    protected string notTaintedValue = "nottainted";
    protected string connectionString;
    protected string CommandUnsafe;
    protected string ScalarCommandUnsafe;
    protected string CommandSafe;
    protected string CommandSafeParam;
    protected string QueryUnsafe;
    protected string QuerySafe;

    protected string CommandUnsafe2;
    protected DbConnection dbConnection;
    protected static string databaseName = "InstrumentationTestsDB";

    public DapperTests()
    {
        dbConnection = SqliteDDBBCreator.Create();
        AddTainted(taintedValue);
        QueryUnsafe = "SELECT * from books where Title = '" + taintedValue + "'";
        QuerySafe = "SELECT * from books where Title = '" + notTaintedValue + "'";
        CommandUnsafe = "Update books set Title = Title where Title ='" + taintedValue + "'";
        CommandSafe = "Update books set Title = Title where Title ='" + notTaintedValue + "'";
    }

    public override void Dispose()
    {
        dbConnection?.Close();
        dbConnection = null;
        base.Dispose();
    }

    [Fact]
    public void GivenDapper_WhenCallingExecuteReaderWithTainted3Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteReader(QueryUnsafe, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenDapper_WhenCallingExecuteReaderWithTaintedDatabaseConnection3Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteReader(QueryUnsafe, null, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenDapper_WhenCallingExecuteScalarCommandWithTaintedDatabaseConnection5Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteScalar(CommandUnsafe, null, null, null, CommandType.Text);
        AssertVulnerable();
    }

    [Fact]
    public void GivenDapper_WhenCallingExecuteReaderWithTaintedDatabaseConnection5Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteReader(QueryUnsafe, null, null, 10000, CommandType.Text);
        AssertVulnerable();
    }

    [Fact]
    public void GivenDapper_WhenCallingExecuteWithTaintedDatabaseConnection2Params_VulnerabilityIsReported()
    {
        dbConnection.Execute(CommandUnsafe, null);
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteWithTaintedDatabaseConnection3Params_VulnerabilityIsReported()
    {
        dbConnection.Execute(CommandUnsafe, null, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenDapper_WhenCallingExecuteWithTaintedDatabaseConnection5Params_VulnerabilityIsReported()
    {
        dbConnection.Execute(CommandUnsafe, null, null, 32423423, CommandType.Text);
        AssertVulnerable();
    }

    [Fact]
    public void GivenDapper_WhenCallingExecuteReaderCommandDefinitionWithTainted_VulnerabilityIsReported()
    {
        dbConnection.ExecuteReader(new CommandDefinition(QueryUnsafe));
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteReaderCommandDefinitionWithTainted_VulnerabilityIsReported2()
    {
        dbConnection.ExecuteReader(new CommandDefinition(QueryUnsafe), CommandBehavior.SchemaOnly);
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteReaderAsyncCommandDefinitionWithTainted_VulnerabilityIsReported()
    {
        dbConnection.ExecuteReaderAsync(new CommandDefinition(QueryUnsafe)).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteReaderAsyncWithTaintedCommandDefinition2Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteReaderAsync(new CommandDefinition(QueryUnsafe), CommandBehavior.Default).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteScalarWithTainted_VulnerabilityIsReported()
    {
        dbConnection.ExecuteScalar(new CommandDefinition(CommandUnsafe));
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteScalarWithTaintedDatabaseConnection2Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteScalar<TestDb.Person>(new CommandDefinition(CommandUnsafe));
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteScalarAsyncWithTainted_VulnerabilityIsReported()
    {
        dbConnection.ExecuteScalarAsync(new CommandDefinition(CommandUnsafe)).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteScalarAsyncWithTaintedDatabaseConnection2Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteScalarAsync<TestDb.Person>(new CommandDefinition(CommandUnsafe)).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteReaderWithTaintedDatabaseConnection_VulnerabilityIsReported()
    {
        dbConnection.ExecuteReader(QueryUnsafe);
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteReaderWithTaintedDatabaseConnection4Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteReader(QueryUnsafe, null, null, 10000);
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteReaderAsyncWithTainted_VulnerabilityIsReported()
    {
        dbConnection.ExecuteReaderAsync(QueryUnsafe).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteReaderAsyncWithTainted2Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteReaderAsync(QueryUnsafe, null).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteReaderAsyncWithTainted3Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteReaderAsync(QueryUnsafe, null, null).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteReaderAsyncWithTainted4Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteReaderAsync(QueryUnsafe, null, null, 1000).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteReaderAsyncWithTainted5Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteReaderAsync(QueryUnsafe, null, null, 1000, CommandType.Text).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteReaderAsyncStringWithTainted2Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteReaderAsync(QueryUnsafe, CancellationToken.None).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteReaderAsyncStringWithTainted3Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteReaderAsync(QueryUnsafe, CancellationToken.None, null).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteReaderStringAsyncWithTainted4Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteReaderAsync(QueryUnsafe, CancellationToken.None, null, null).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteReaderAsyncStringWithTainted5Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteReaderAsync(QueryUnsafe, CancellationToken.None, null, null, CommandType.Text).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteReaderAsyncWithTaintedDatabaseConnection4Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteReaderAsync(QueryUnsafe, CancellationToken.None, null, 23323).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteReaderAsyncWithTaintedDatabaseConnection5Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteReaderAsync(QueryUnsafe, CancellationToken.None, null, 23323, CommandType.Text).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteCommandScalarWithTainted_VulnerabilityIsReported()
    {
        dbConnection.ExecuteScalar(CommandUnsafe);
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteScalarCommandWithTaintedDatabaseConnection2Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteScalar(CommandUnsafe, null);
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteScalarCommandWithTaintedDatabaseConnection3Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteScalar(CommandUnsafe, null, null);
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteScalarCommandWithTaintedDatabaseConnection4Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteScalar(CommandUnsafe, null, null, null);
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteScalarAsyncStringWithTainted_VulnerabilityIsReported()
    {
        dbConnection.ExecuteScalarAsync(CommandUnsafe).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteScalarAsyncStringWithTaintedDatabaseConnection2Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteScalarAsync(CommandUnsafe, null).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteScalarAsyncWithTainted3Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteScalarAsync(CommandUnsafe, null, null);
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteScalarAsyncWithTaintedDatabaseConnection4Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteScalarAsync(CommandUnsafe, null, null, 100000).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteScalarAsyncWithTaintedDatabaseConnection5Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteScalarAsync(CommandUnsafe, null, null, 100000, CommandType.Text).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteScalarAsyncWithTaintedParamsCancellation_VulnerabilityIsReported()
    {
        dbConnection.ExecuteScalarAsync(CommandUnsafe, CancellationToken.None).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteScalarStringAsyncWithTainted3Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteScalarAsync(CommandUnsafe, CancellationToken.None, null).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteScalarStringAsyncWithTaintedDatabaseConnection4Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteScalarAsync(CommandUnsafe, CancellationToken.None, null, 10000).Wait();
        AssertVulnerable();
    }


    [Fact]
    
    public void GivenDapper_WhenCallingExecuteScalarStringAsyncWithTaintedDatabaseConnection5Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteScalarAsync(CommandUnsafe, CancellationToken.None, null, 10000, CommandType.Text).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingQueryMultipleAsyncWithTainted_VulnerabilityIsReported()
    {
        dbConnection.QueryMultipleAsync(CommandUnsafe).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingQueryMultipleAsyncWithTaintedDatabaseConnection2Params_VulnerabilityIsReported()
    {
        dbConnection.QueryMultipleAsync(CommandUnsafe, null).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingQueryMultipleAsyncWithTaintedDatabaseConnection3Params_VulnerabilityIsReported()
    {
        dbConnection.QueryMultipleAsync(CommandUnsafe, null, null).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingQueryMultipleAsyncWithTaintedDatabaseConnection4Params_VulnerabilityIsReported()
    {
        dbConnection.QueryMultipleAsync(CommandUnsafe, null, null, 23423423).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingQueryMultipleAsyncWithTaintedDatabaseConnection5Params_VulnerabilityIsReported()
    {
        dbConnection.QueryMultipleAsync(CommandUnsafe, null, null, 32423423, CommandType.Text).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteQueryMultipleAsyncWithTainted_VulnerabilityIsReported()
    {
        dbConnection.QueryMultipleAsync(new CommandDefinition(CommandUnsafe)).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingQueryAsyncWithTainted_VulnerabilityIsReported()
    {
        dbConnection.QueryAsync(CommandUnsafe).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingQueryAsyncWithTaintedDatabaseConnection2Params_VulnerabilityIsReported()
    {
        dbConnection.QueryAsync(CommandUnsafe, null).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingQueryAsyncWithTaintedDatabaseConnection3Params_VulnerabilityIsReported()
    {
        dbConnection.QueryAsync(CommandUnsafe, null, null).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingQueryAsyncWithTaintedDatabaseConnection4Params_VulnerabilityIsReported()
    {
        dbConnection.QueryAsync(CommandUnsafe, null, null, 23423423).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingQueryAsyncWithTaintedDatabaseConnection5Params_VulnerabilityIsReported()
    {
        dbConnection.QueryAsync(CommandUnsafe, null, null, 32423423, CommandType.Text).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteQueryAsyncWithTainted_VulnerabilityIsReported()
    {
        dbConnection.QueryAsync(new CommandDefinition(CommandUnsafe)).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteAsyncWithTainted_VulnerabilityIsReported()
    {
        dbConnection.ExecuteAsync(CommandUnsafe).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteAsyncWithTaintedDatabaseConnection2Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteAsync(CommandUnsafe, null).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteAsyncWithTaintedDatabaseConnection3Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteAsync(CommandUnsafe, null, null).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteAsyncWithTaintedDatabaseConnection4Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteAsync(CommandUnsafe, null, null, 23423423).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteAsyncWithTaintedDatabaseConnection5Params_VulnerabilityIsReported()
    {
        dbConnection.ExecuteAsync(CommandUnsafe, null, null, 32423423, CommandType.Text).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteExecuteAsyncWithTainted_VulnerabilityIsReported()
    {
        dbConnection.ExecuteAsync(new CommandDefinition(CommandUnsafe)).Wait();
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteWithTainted_VulnerabilityIsReported()
    {
        dbConnection.Execute(CommandUnsafe);
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteWithTaintedDatabaseConnection4Params_VulnerabilityIsReported()
    {
        dbConnection.Execute(CommandUnsafe, null, null, 23423423);
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingExecuteExecuteWithTainted_VulnerabilityIsReported()
    {
        dbConnection.Execute(new CommandDefinition(CommandUnsafe));
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingQueryWithTainted_VulnerabilityIsReported()
    {
        dbConnection.Query<TestDb.Person>(CommandUnsafe);
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingQueryWithTaintedDatabaseConnection2Params_VulnerabilityIsReported()
    {
        dbConnection.Query<TestDb.Person>(CommandUnsafe, null);
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingQueryWithTaintedDatabaseConnection5Params_VulnerabilityIsReported()
    {
        dbConnection.Query<TestDb.Person>(CommandUnsafe, null, null, true, 343434);
        AssertVulnerable();
    }

    [Fact]
    
    public void GivenDapper_WhenCallingQueryQueryWithTainted_VulnerabilityIsReported()
    {
        dbConnection.Query<TestDb.Person>(new CommandDefinition(CommandUnsafe));
        AssertVulnerable();
    }
}
