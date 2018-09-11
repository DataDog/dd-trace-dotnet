using System;
using System.Linq;

namespace Samples.SqlServer
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var db = new BloggingContext())
            {
                // create database if missing
                db.Database.EnsureCreated();

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
    }
}
