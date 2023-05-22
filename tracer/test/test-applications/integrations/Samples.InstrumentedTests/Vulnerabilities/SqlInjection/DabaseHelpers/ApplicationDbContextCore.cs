#if NETCOREAPP3_0_OR_GREATER

using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class ApplicationDbContextCore : DbContext
{
    readonly DbConnection connection = null;
    readonly string connectionString;

    readonly bool isSqlLite = false;
    public ApplicationDbContextCore(string connectionString, bool isSqlLite = false)
    {
        this.connectionString = connectionString;
        this.isSqlLite = isSqlLite;
    }

    public ApplicationDbContextCore(DbConnection connection, bool isSqlLite = false)
    {
        this.connection = connection;
        this.isSqlLite = isSqlLite;
    }

    public ApplicationDbContextCore(DbContextOptions<ApplicationDbContextCore> options)
        : base(options)
    {
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!isSqlLite)
        {
            if (connection != null)
            {
                options.UseSqlServer(connection);
            }
            else
            {
                options.UseSqlServer(connectionString);
            }
        }
        else
        {
            if (connection != null)
            {
                options.UseSqlite(connection);
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        }
    }

    public Microsoft.EntityFrameworkCore.DbSet<Book> Books { get; set; }

}
#endif
