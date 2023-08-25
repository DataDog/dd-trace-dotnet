#if !NETCOREAPP2_1
using System.Data;
using System.Data.Entity.Core.EntityClient;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using DelegateDecompiler;
using FluentAssertions;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

// We cannot use localDB on linux and these calls cannot be mocked
[Trait("Category", "LinuxUnsupported")]
public class EFTests : EFBaseTests
{
    public EFTests()
    {
        var connection = SqlDDBBCreator.Create();
        db = new ApplicationDbContext(connection.ConnectionString);
        titleParam = new SqlParameter("@title", taintedTitle);

        if (db.Database.Connection.State != ConnectionState.Open)
        {
            db.Database.Connection.Open();
        }
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecutSqlQueryWithTainted_VulnerabilityIsReported()
    {
        var data = (db as ApplicationDbContext).Books.SqlQuery(queryUnsafe).ToList();
        data.Count.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecutSqlQueryParamWithTainted_VulnerabilityIsNotReported()
    {
        var data = (db as ApplicationDbContext).Books.SqlQuery(@"Select * from dbo.Books where title =@title", titleParam).ToList();
        data.Count.Should().Be(1);
        AssertNotVulnerable();
    }

    protected override EntityCommand GetEntityCommand(string title)
    {
        var queryString = "SELECT b.Title FROM ApplicationDbContext.Books AS b where b.Title ='" + title + "'";
        var adapter = (IObjectContextAdapter)db;
        var objectContext = adapter.ObjectContext;
        var conn = (EntityConnection)objectContext.Connection;
        conn.Open();
        var query = new EntityCommand(queryString, conn);
        return query;
    }

    [Fact]
    public void TestDelegateDecompileLibBug()
    {
        var book = new Book { Id = "id", Title = "title", Author = "author" };
        var ft = book.FullTitle;
        var books = (db as ApplicationDbContext).Books;
        var decompiled = books.Decompile();
        var any = decompiled.Any(x => x.FullTitle != ft);
        //var data = (db as ApplicationDbContext).Books.Decompile().Where(x => x.FullTitle != "eeef").ToList();
        if (any)
        {

        }
    }

}
#endif
