#if NETCOREAPP3_0_OR_GREATER

using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class ApplicationDbContextCore : DbContext
{
    readonly DbConnection connection = null;
    readonly bool isSqlLite = false;

    public ApplicationDbContextCore(DbConnection connection, bool isSqlLite = false)
    {
        this.connection = connection;
        this.isSqlLite = isSqlLite;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!isSqlLite)
        {
            options.UseSqlServer(connection);
        }
        else
        {
            options.UseSqlite(connection);
        }
    }

    public Microsoft.EntityFrameworkCore.DbSet<Book> Books { get; set; }

}
#endif
