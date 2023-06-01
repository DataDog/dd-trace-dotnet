using System.Data.SQLite;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

internal class SqliteDDBBCreator
{
    public static SQLiteConnection Create()
    {
        var builder = new SQLiteConnectionStringBuilder();
        builder.DataSource = ":memory:";
        var conn = builder.ConnectionString;

        if (string.IsNullOrEmpty(conn))
        {
            throw new System.Exception("Cannot create sqlite database.");
        }

        var dbConnection = new SQLiteConnection(conn);
        dbConnection.Open();

        foreach (var command in DDBBTestHelper.GetCommands())
        {
            new SQLiteCommand(command, dbConnection).ExecuteReader();
        }

        return (dbConnection);
    }
}
