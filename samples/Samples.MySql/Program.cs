using System;
using Datadog.Trace.ClrProfiler;
using MySql.Data.MySqlClient;

namespace Samples.MySql
{
    class Program
    {
        private static string Host()
        {
            return Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "localhost";
        }

        private static string ConnectionString(string database)
        {
            return $"server={Host()};user=mysqldb;password=mysqldb;port=3306;database={database}";
        }

        private static void Main(string[] args)
        {
            Console.WriteLine($"Profiler attached: {Instrumentation.ProfilerAttached}");
            Console.WriteLine($"Platform: {(Environment.Is64BitProcess ? "x64" : "x32")}");
            Console.WriteLine();

            Console.WriteLine("Opening the connection.");

            string connStr = ConnectionString("world");
            var conn = new MySqlConnection(connStr);
            conn.Open();

            Console.WriteLine("Creating the table for the continents.");

            // Create table
            var tableCommand =
                new MySqlCommand(
                    "DROP TABLE IF EXISTS `continent`; CREATE TABLE continent (continent_id INT AUTO_INCREMENT, name VARCHAR(255) NOT NULL, PRIMARY KEY(continent_id));",
                    conn);
            tableCommand.ExecuteNonQuery();

            Console.WriteLine("Creating the continents.");

            // Create continents
            MySqlCommand createContinent;
            createContinent = new MySqlCommand("INSERT INTO continent (name) VALUES ('Africa');", conn);
            createContinent.ExecuteNonQuery();
            createContinent = new MySqlCommand("INSERT INTO continent (name) VALUES ('Antarctica');", conn);
            createContinent.ExecuteNonQuery();
            createContinent = new MySqlCommand("INSERT INTO continent (name) VALUES ('Asia');", conn);
            createContinent.ExecuteNonQuery();
            createContinent = new MySqlCommand("INSERT INTO continent (name) VALUES ('Australia');", conn);
            createContinent.ExecuteNonQuery();
            createContinent = new MySqlCommand("INSERT INTO continent (name) VALUES ('Europe');", conn);
            createContinent.ExecuteNonQuery();
            createContinent = new MySqlCommand("INSERT INTO continent (name) VALUES ('North America');", conn);
            createContinent.ExecuteNonQuery();
            createContinent = new MySqlCommand("INSERT INTO continent (name) VALUES ('South America');", conn);
            createContinent.ExecuteNonQuery();

            try
            {
                Console.WriteLine("Beginning to read the continents.");
                string sql = "SELECT continent_id, name FROM continent";
                var readContinents = new MySqlCommand(sql, conn);
                using (var rdr = readContinents.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        Console.WriteLine(rdr[0] + " -- " + rdr[1]);
                    }
                    rdr.Close();
                }
                Console.WriteLine("Done reading the continents.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            conn.Close();
            Console.WriteLine("Done setting up, inserting, and reading from MySQL.");
        }
    }
}
