using System;
using System.Data.Common;
using System.Linq;
using Datadog.Trace.ClrProfiler;
using Npgsql;

namespace Samples.Npgsql
{
    public static class Program
    {
        private static string Host()
        {
            return Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
        }

        private static string ConnectionString(string database)
        {
            return $"Host={Host()};Username=postgres;Password=postgres;Database={database}";
        }

        public static void Main(string[] args)
        {
            Console.WriteLine($"Profiler attached: {Instrumentation.ProfilerAttached}");
            Console.WriteLine($"Platform: {(Environment.Is64BitProcess ? "x64" : "x32")}");
            Console.WriteLine();

            using (var conn = new NpgsqlConnection(ConnectionString("postgres")))
            {
                conn.Open();

                using (var createTable = conn.CreateCommand())
                {
                    createTable.CommandText = @"
    DROP TABLE IF EXISTS employee;
    CREATE TABLE employee (
        employee_id SERIAL,  
        name varchar(45) NOT NULL,  
        birth_date varchar(450) NOT NULL,  
      PRIMARY KEY (employee_id)  
    )";

                    createTable.ExecuteNonQuery();
                }

                // Insert some data
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO employee (name, birth_date) VALUES (@name, @birth_date);";
                    cmd.Parameters.AddWithValue("name", "Jane Smith");
                    cmd.Parameters.AddWithValue("@birth_date", new DateTime(1980, 2, 3));

                    int count = cmd.ExecuteNonQuery();
                    Console.WriteLine($"{count} rows inserted.");
                }

                // Retrieve all rows
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM employee sync;";
                    int rows = 0;

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var values = new object[10];
                            int count = reader.GetValues(values);
                            Console.WriteLine(string.Join(", ", values.Take(count)));
                            rows++;
                        }
                    }

                    Console.WriteLine($"{rows:N0} rows returned from sync query.");
                    Console.WriteLine();

                    cmd.CommandText = "SELECT * FROM employee async;";
                    rows = 0;

                    using (var reader = cmd.ExecuteReaderAsync().GetAwaiter().GetResult())
                    {
                        while (reader.Read())
                        {
                            var values = new object[10];
                            int count = reader.GetValues(values);
                            Console.WriteLine(string.Join(", ", values.Take(count)));
                            rows++;
                        }
                    }

                    Console.WriteLine($"{rows:N0} rows returned from async query.");
                }

                // Delete all data
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM employee;";

                    int count = cmd.ExecuteNonQuery();
                    Console.WriteLine($"{count} rows deleted.");
                }
            }
        }
    }
}
