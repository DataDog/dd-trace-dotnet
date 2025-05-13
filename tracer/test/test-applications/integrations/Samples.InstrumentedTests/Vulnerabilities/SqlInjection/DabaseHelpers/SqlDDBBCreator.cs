using System;
using System.Data.SqlClient;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

internal static class SqlDDBBCreator
{
    static object DDBBLock = new();
    static bool DDBBCreated = false;
    public static SqlConnection Create()
    {
        // Linux does not support localDB
        if (!InstrumentationTestsBase.IsWindows())
        {
            return new SqlConnection("Data Source=.\\DUMMY;Initial Catalog=Dummy");
        }

        var connection = OpenConnection();

        lock (DDBBLock)
        {
            if (!DDBBCreated)
            {
                var dropTablesCommand = "EXEC sp_MSforeachtable 'DROP TABLE ?'";
                new SqlCommand(dropTablesCommand, connection).ExecuteNonQuery();

                foreach (var command in DDBBTestHelper.GetCommands())
                {
                    ExecuteCommand(connection, command);
                }

                DDBBCreated = true;
            }
        }
        
        return connection;
    }

    private static void ExecuteCommand(SqlConnection connection, string sql)
    {
        using (var command1 = new SqlCommand(sql, connection))
        {
            command1.ExecuteNonQuery();
        }
    }

    private static SqlConnection OpenConnection()
    {
        int numAttempts = 3;
        var connectionString = @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Connection Timeout=60";

        for (int i = 0; i < numAttempts; i++)
        {
            SqlConnection connection = null;

            try
            {
                connection = Activator.CreateInstance(typeof(SqlConnection), connectionString) as SqlConnection;
                connection.Open();
                return connection;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                connection?.Dispose();
            }
        }

        throw new Exception($"Unable to open connection to connection string {connectionString} after {numAttempts} attempts");
    }
}
