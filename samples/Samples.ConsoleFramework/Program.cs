using System;
using System.Collections;
using System.Data.SqlClient;
using System.Linq;
using Newtonsoft.Json;

namespace Samples.ConsoleFramework
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            object output = new Program().Run();

            var serializer = JsonSerializer.CreateDefault();
            serializer.Formatting = Formatting.Indented;
            serializer.Serialize(Console.Out, output);

            Console.WriteLine();
        }

        private object Run()
        {
            var prefixes = new[] { "COR_", "CORECLR_", "DATADOG_" };

            var environmentVariables = from entry in Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
                                       from prefix in prefixes
                                       let key = ((string)entry.Key).ToUpperInvariant()
                                       where key.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase)
                                       select new
                                       {
                                           Key = key,
                                           entry.Value
                                       };

            var dictionary = environmentVariables.ToDictionary(v => v.Key, v => v.Value);
            int addResult = new ExampleLibrary.Class1().Add(1, 2);

            dictionary.Add("ProfilerAttached", Datadog.Trace.ClrProfiler.Instrumentation.ProfilerAttached);
            dictionary.Add("AddResult", addResult);


            using (var db = new BloggingContext())
            {
                // Create and save a new Blog
                var name = "test-1";

                var blog = new Blog { Name = name };
                db.Blogs.Add(blog);
                db.SaveChanges();

                // Display all Blogs from the database
                var query = from b in db.Blogs
                            orderby b.Name
                            select b;

                Console.WriteLine("All blogs in the database:");
                foreach (var item in query)
                {
                    Console.WriteLine(item.Name);
                }

                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }


            return dictionary;
        }
    }
}
