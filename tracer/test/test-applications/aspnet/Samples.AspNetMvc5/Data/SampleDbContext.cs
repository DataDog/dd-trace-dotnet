using System.Data.Entity;

namespace Samples.AspNetMvc5.Data
{
    public class SampleDbContext : DbContext
    {
        static SampleDbContext()
        {
            Database.SetInitializer(new DropCreateDatabaseIfModelChanges<SampleDbContext>());
        }

        public DbSet<Contact> Contacts { get; set; }
    }


}
