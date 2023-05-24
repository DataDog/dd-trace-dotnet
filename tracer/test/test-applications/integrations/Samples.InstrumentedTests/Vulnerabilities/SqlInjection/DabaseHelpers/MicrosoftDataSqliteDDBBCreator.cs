using Microsoft.Data.Sqlite;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public static class MicrosoftDataSqliteDdbbCreator
{
    public static SqliteConnection Create()
    {
        var builder = new SqliteConnectionStringBuilder();
        builder.DataSource = ":memory:";
        var conn = builder.ConnectionString;

        if (string.IsNullOrEmpty(conn))
        {
            throw new System.Exception("Cannot create sqlite database.");
        }

        var dbConnection = new SqliteConnection(conn);
        dbConnection.Open();

        foreach (var command in DDBBTestHelper.GetCommands())
        {
            new SqliteCommand(command, dbConnection).ExecuteReader();
        }

        return (dbConnection);
    }
}
