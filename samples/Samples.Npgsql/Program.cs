using System;
using System.Data.Common;
using System.Linq;
using Datadog.Trace.ClrProfiler;
using Npgsql;

namespace Samples.Npgsql
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine($"Profiler attached: {Instrumentation.ProfilerAttached}");
            Console.WriteLine($"Platform: {(Environment.Is64BitProcess ? "x64" : "x32")}");
            Console.WriteLine();

            using (var conn = new NpgsqlConnection("Host=localhost;Username=postgres;Password=postgres;Database=postgres"))
            {
                conn.Open();

                // Insert some data
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO employees (name, birth_date) VALUES (@name, @birth_date);";
                    cmd.Parameters.AddWithValue("name", "Jane Smith");
                    cmd.Parameters.AddWithValue("@birth_date", new DateTime(1980, 2, 3));

                    int count = cmd.ExecuteNonQuery();
                    Console.WriteLine($"{count} rows inserted.");
                }

                // Retrieve all rows
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM employees sync;";
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

                    cmd.CommandText = "SELECT * FROM employees async;";
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
                    cmd.CommandText = "DELETE FROM employees;";

                    int count = cmd.ExecuteNonQuery();
                    Console.WriteLine($"{count} rows deleted.");
                }
            }
        }
    }
}
