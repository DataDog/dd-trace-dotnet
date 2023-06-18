#if !NETCOREAPP2_1
using System.Data.Entity;
using System.Data.Entity.Core.Common;
using System.Data.SQLite;
using System.Data.SQLite.EF6;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class ApplicationDbSqlLiteContext : DbContext
{
    public class SQLiteConfiguration : DbConfiguration
    {
        public SQLiteConfiguration()
        {
            SetProviderFactory("System.Data.SQLite", SQLiteFactory.Instance);
            SetProviderFactory("System.Data.SQLite.EF6", SQLiteProviderFactory.Instance);
            SetProviderServices("System.Data.SQLite", (DbProviderServices)SQLiteProviderFactory.Instance.GetService(typeof(DbProviderServices)));
        }
    }

    public ApplicationDbSqlLiteContext(SQLiteConnection conn) : 
        base(conn, true)
    {
        DbConfiguration.SetConfiguration(new SQLiteConfiguration());
    }

    public DbSet<Book> Books { get; set; }

    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Book>().ToTable("Books");
    }
}
#endif
