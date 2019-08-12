using System.Data.Entity;

namespace Samples.WebApi.Async.EntityFramework
{
    public class CodeFirstContext : DbContext
    {
        public CodeFirstContext()
        {
            Database.SetInitializer(new DropCreateDatabaseAlways<CodeFirstContext>());
        }

        public DbSet<Joke> Jokes { get; set; }
    }
}
