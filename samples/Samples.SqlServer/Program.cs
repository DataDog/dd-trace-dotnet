using System;
using System.Linq;

namespace Samples.SqlServer
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var db = GetConnection())
            {
                var name = "test";

                var blog = (from b in db.Blogs where b.Name == name select b).FirstOrDefault();
                if (blog == null)
                {
                    blog = new Blog { Name = name };
                    db.Blogs.Add(blog);
                    db.SaveChanges();
                }

                // Display all Blogs from the database
                var query = from b in db.Blogs
                            orderby b.Name
                            select b;

                Console.WriteLine("All blogs in the database:");
                foreach (var item in query)
                {
                    Console.WriteLine(item.Name);
                }
            }
        }

        private static BloggingContext GetConnection()
        {
            var cs = Environment.GetEnvironmentVariable("CONNECTION_STRING");
            if (string.IsNullOrEmpty(cs))
            {
                Console.WriteLine("using default database");
                return new BloggingContext();
            } else
            {
                Console.WriteLine($"using datatabase: {cs}");
                return new BloggingContext(cs);
            }
        }
    }
}
