using System;
using MySql.Data.MySqlClient;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

internal static class MySqlDDBBCreator
{
    static object DDBBLock = new();
    static bool DDBBCreated = false;
    public const string connectionString = "server=localhost;port=3306;user id=root;password=root;database=testDb";
    public static MySqlConnection Create()
    {
        var connection = OpenConnection();

        if (connection is not null)
        {
            lock (DDBBLock)
            {
                if (!DDBBCreated)
                {
                    var dropTablesCommand = "DROP TABLE IF EXISTS Persons, Books;";
                    new MySqlCommand(dropTablesCommand, connection).ExecuteNonQuery();

                    foreach (var command in DDBBTestHelper.GetCommands())
                    {
                        ExecuteCommand(connection, command);
                    }

                    DDBBCreated = true;
                }
            }
        }
        
        return connection ?? new MySqlConnection("server=DUMMY;port=3306;user id=DUMMY;password=DUMMY;database=DUMMY");
    }

    private static void ExecuteCommand(MySqlConnection connection, string booksCommand)
    {
        using (var command1 = new MySqlCommand(booksCommand, connection))
        {
            using (var reader1 = command1.ExecuteReader())
            {
                reader1.Close();
            }
        }
    }

    private static MySqlConnection OpenConnection()
    {
        int numAttempts = 3;

        for (int i = 0; i < numAttempts; i++)
        {
            MySqlConnection connection = null;

            try
            {
                connection = Activator.CreateInstance(typeof(MySqlConnection), connectionString) as MySqlConnection;
                connection.Open();
                return connection;
            }
            catch (Exception)
            {
                connection?.Dispose();
            }
        }

        return null;
    }
}
