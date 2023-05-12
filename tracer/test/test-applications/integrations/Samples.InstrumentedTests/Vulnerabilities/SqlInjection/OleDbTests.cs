using System.Data;
using System.Data.OleDb;
using System.Threading;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;
public class OleDbTests : InstrumentationTestsBase
{
    protected string taintedValue = "tainted";
    protected string notTaintedValue = "nottainted";
    protected string connectionString;
    protected string CommandUnsafe;
    protected string CommandSafe;
    protected string CommandSafeParam;

    protected string CommandUnsafe2;
    protected OleDbConnection dbConnection;
    protected static string databaseName = "InstrumentationTestsDB";

    public OleDbTests()
    {
        // connectionString = "Provider=SQLOLEDB;" + SqlServerInitializer.InitDatabase();
#if NETCOREAPP2_1_OR_GREATER
        var connection = MicrosoftDataSqliteDdbbCreator.Create();
        dbConnection = new OleDbConnection(connectionString);
        dbConnection.Open();
        AddTainted(taintedValue);
#endif
        CommandUnsafe = "SELECT * from Persons where Name like '" + taintedValue + "'";
        CommandUnsafe2 = "Update Persons set Name = Name where Name ='" + taintedValue + "'";
        CommandSafe = "SELECT * from Persons where Name like 'nottainted'";
    }

    public void OleDbTestCleanup()
    {
        dbConnection?.Close();
        dbConnection = null;
    }

    protected OleDbCommand DbCommand(string commandTxt, object param = null)
    {
        var dbCommand = dbConnection.CreateCommand();
        dbCommand.CommandText = commandTxt;
        if (param != null)
        {
            dbCommand.Parameters.Add(param);
        }
        return dbCommand;
    }

    protected OleDbDataReader DataReader()
    {
        return DbCommand("Select * From Persons").ExecuteReader();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteReaderWithTainted_VulnerabilityIsReported2()
    {
        using (DbCommand(CommandUnsafe).ExecuteReader(CommandBehavior.Default))
        {
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteReaderWithTainted_VulnerabilityIsReported()
    {
        using (DbCommand(CommandUnsafe).ExecuteReader())
        {
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported()
    {
        _ = DbCommand(CommandUnsafe).ExecuteNonQuery();
        AssertVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteNonQueryWithNotTainted_VulnerabilityIsNotReported()
    {
        _ = DbCommand(CommandSafe).ExecuteNonQuery();
        AssertNotVulnerable();
    }

    // [ExpectedException(typeof(OleDbException))]
    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteNonQueryWithNull_VulnerabilityIsNotReported()
    {
        _ = DbCommand(null).ExecuteNonQuery();
        AssertVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteNonQueryAsyncWithTaintedCancellation_VulnerabilityIsReported()
    {
        _ = DbCommand(CommandUnsafe).ExecuteNonQueryAsync(CancellationToken.None).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteNonQueryAsyncWithTaintedCancellation_VulnerabilityIsReported2()
    {
        _ = DbCommand(CommandUnsafe).ExecuteNonQueryAsync().Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteScalarAsyncWithTaintedCancellation_VulnerabilityIsReported()
    {
        _ = DbCommand(CommandUnsafe).ExecuteScalarAsync(CancellationToken.None).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteScalarAsyncWithTaintedCancellation_VulnerabilityIsReported2()
    {
        _ = DbCommand(CommandUnsafe).ExecuteScalarAsync().Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteReaderAsyncWithTaintedCancellation_VulnerabilityIsReported()
    {
        using (DbCommand(CommandUnsafe).ExecuteReaderAsync(CancellationToken.None).Result)
        {
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteReaderAsyncWithTaintedCancellation_VulnerabilityIsReported2()
    {
        using (DbCommand(CommandUnsafe).ExecuteReaderAsync(CommandBehavior.Default, CancellationToken.None).Result)
        {
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteReaderAsyncWithTaintedCancellation_VulnerabilityIsReported3()
    {
        using (DbCommand(CommandUnsafe).ExecuteReaderAsync(CommandBehavior.Default).Result)
        {
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteReaderAsyncWithTaintedCancellation_VulnerabilityIsReported4()
    {
        using (DbCommand(CommandUnsafe).ExecuteReaderAsync().Result)
        {
            AssertVulnerable();
        }
    }

    [Fact]
    public void GivenASqlCommand_WhenCallingExecuteScalarWithTainted_VulnerabilityIsReported()
    {
        _ = DbCommand(CommandUnsafe).ExecuteScalar();
        AssertVulnerable();

    }

    [Fact]
    public void GivenADbDataReader_WhenCallingGetString_ValueIsTainted()
    {
        using (var dbDataReader = DataReader())
        {
            dbDataReader.Read();
            var value = dbDataReader.GetString(2);
            AssertTainted(value);
        }
    }

    [Fact]
    public void GivenADbDataReader_WhenCallingGetValue_ValueIsTainted()
    {
        using (var dbDataReader = DataReader())
        {
            dbDataReader.Read();
            var value = dbDataReader.GetValue(2);
            AssertTainted(value);
        }
    }

    [Fact]
    public void GivenADbDataReader_WhenCallingGetItem_ValueIsTainted()
    {
        using (var dbDataReader = DataReader())
        {
            dbDataReader.Read();
            var value = dbDataReader[2];
            AssertTainted(value);
        }
    }

    [Fact]
    public void GivenADbDataReader_WhenCallingGetItemString_ValueIsTainted()
    {
        using (var dbDataReader = DataReader())
        {
            dbDataReader.Read();
            var value = dbDataReader["Name"];
            AssertTainted(value);
        }
    }
}
