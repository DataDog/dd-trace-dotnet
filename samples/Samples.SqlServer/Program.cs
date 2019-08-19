using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;

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

                // Display all Blogs from the database syncronously
                var query = from b in db.Blogs
                            orderby b.Name
                            select b;

                Console.WriteLine("All blogs in the database from the synchronous call:");
                foreach (var item in query)
                {
                    Console.WriteLine(item.Name);
                }

                var asyncName = "test-async";

                var asyncBlog = (from b in db.Blogs where b.Name == asyncName select b).FirstOrDefaultAsync();
                if (asyncBlog == null)
                {
                    blog = new Blog { Name = asyncName };
                    db.Blogs.Add(blog);
                    db.SaveChangesAsync().Wait();
                }

                // Display all Blogs from the database asynchronously
                var asyncQueryTask = db.Blogs.Where(b => b.Name == asyncName).ToListAsync();

                asyncQueryTask.Wait();

                Console.WriteLine("All blogs in the database from the async call:");
                foreach (var item in asyncQueryTask.Result)
                {
                    Console.WriteLine(item.Name);
                }
            }
        }
    }
}
