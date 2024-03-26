#if !NETCOREAPP2_1
using System.Data.Entity;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(string conn) :
        base(conn)
    {
    }

    public System.Data.Entity.DbSet<Book> Books { get; set; }

    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Book>().ToTable("Books", "dbo");
    }
}
#endif
