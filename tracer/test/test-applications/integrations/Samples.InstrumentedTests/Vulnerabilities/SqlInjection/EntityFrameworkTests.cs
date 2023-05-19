#if !NETCOREAPP2_1
using System;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Core.EntityClient;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using FluentAssertions;
using LinqToDB.Data.DbCommandProcessor;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class EntityFrameworkTests : InstrumentationTestsBase, IDisposable
{
    protected string taintedTitle = "CLR via C#";
    protected string notTaintedValue = "nottainted";
    string CommandUnsafeText;
    readonly string CommandSafe = "Update dbo.Books set title= title where title = @title";
    SqlParameter titleParam;
    string queryUnsafe;
    private ApplicationDbContext db;

    public EntityFrameworkTests()
    {
        var connection = SqlDDBBCreator.Create();
        db = new ApplicationDbContext(connection.ConnectionString);

        if (db.Database.Connection.State != ConnectionState.Open)
        {
            db.Database.Connection.Open();
        }
        AddTainted(taintedTitle);
        CommandUnsafeText = "Update Books set title= title where title ='" + taintedTitle + "'";
        queryUnsafe = "SELECT * from Books where title like '" + taintedTitle + "'";
        titleParam = new SqlParameter("@title", taintedTitle);
    }

    public void Dispose()
    {
        db.Database.Connection.Close();
        db.Dispose();
        db = null;
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteSqlCommandWithTainted_VulnerabilityIsReported()
    {
        var result = db.Database.ExecuteSqlCommand(CommandUnsafeText);
        result.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteSqlCommandObjectsWithTainted_VulnerabilityIsReported()
    {
        var result = db.Database.ExecuteSqlCommand(CommandUnsafeText, titleParam);
        result.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteSqlCommandTransactionalBehaviorWithTainted_VulnerabilityIsReported()
    {
        var result = db.Database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction, CommandUnsafeText);
        result.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteSqlCommandTransactionalBehaviorObjectWithTainted_VulnerabilityIsReported()
    {
        var result = db.Database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction, CommandUnsafeText, titleParam);
        result.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteSqlCommandTransactionalBehaviorObjectSafe_VulnerabilityIsNotReported()
    {
        var result = db.Database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction, CommandSafe, titleParam);
        result.Should().Be(1);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteSqlCommandObjectSafe_VulnerabilityIsNotReported()
    {
        var result = db.Database.ExecuteSqlCommand(CommandSafe, titleParam);
        result.Should().Be(1);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteSqlCommandParamSafe_VulnerabilityIsNotReported()
    {
        var result = db.Database.ExecuteSqlCommand(@"Update dbo.Books set title=@title where title =@title", titleParam);
        result.Should().Be(1);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteSqlCommandFormatUnsafe_VulnerabilityIsReported()
    {
        var result = db.Database.ExecuteSqlCommand("Update dbo.Books set title='"+ taintedTitle + "' where title = '" + taintedTitle + "'");
        result.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteSqlCommandAsyncWithTainted_VulnerabilityIsReported()
    {
        var result = db.Database.ExecuteSqlCommandAsync(CommandUnsafeText).Result;
        result.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteSqlCommandAsyncWithTainted_VulnerabilityIsReported2()
    {
        var result = db.Database.ExecuteSqlCommandAsync(CommandUnsafeText, titleParam).Result;
        result.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteSqlCommandAsyncWithTainted_VulnerabilityIsReported3()
    {
        var result = db.Database.ExecuteSqlCommandAsync(TransactionalBehavior.DoNotEnsureTransaction, CommandUnsafeText).Result;
        result.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteSqlCommandAsyncWithTainted_VulnerabilityIsReported4()
    {
        var result = db.Database.ExecuteSqlCommandAsync(TransactionalBehavior.DoNotEnsureTransaction, CommandUnsafeText, titleParam).Result;
        result.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteSqlCommandAsyncWithTainted_VulnerabilityIsReported5()
    {
        var result = db.Database.ExecuteSqlCommandAsync(TransactionalBehavior.DoNotEnsureTransaction, CommandUnsafeText, CancellationToken.None, titleParam).Result;
        result.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteSqlCommandAsyncWithTainted_VulnerabilityIsReported6()
    {
        var result = db.Database.ExecuteSqlCommandAsync(CommandUnsafeText, CancellationToken.None, titleParam).Result;
        result.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteSqlCommandAsyncCancellationTokenSafe_VulnerabilityIsNotReported6()
    {
        var result = db.Database.ExecuteSqlCommandAsync(CommandSafe, CancellationToken.None, titleParam).Result;
        result.Should().Be(1);
        AssertNotVulnerable();
    }


    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteSqlCommandAsyncSafe_VulnerabilityIsNotReported6()
    {
        var result = db.Database.ExecuteSqlCommandAsync(CommandSafe, titleParam).Result;
        result.Should().Be(1);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecutSqlQueryGenericWithTainted_VulnerabilityIsReported()
    {
        var data = db.Database.SqlQuery<Book>(queryUnsafe).ToList();
        data.Count.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecutSqlQuerygenericFormatWithTainted_VulnerabilityIsReported()
    {
        var data = db.Database.SqlQuery<Book>(queryUnsafe).ToList();
        data.Count.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecutSqlQueryGenericParamWithTainted_VulnerabilityIsReported()
    {
        var data = db.Database.SqlQuery<Book>(queryUnsafe + @" and title =@title", titleParam).ToList();
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecutSqlQueryGenericParamWithTainted_VulnerabilityIsNotReported2()
    {
        var data = db.Database.SqlQuery<Book>(@"Select * from dbo.Books where title =@title", titleParam).ToList();
        data.Count.Should().Be(1);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecutSqlQueryWithTainted_VulnerabilityIsReported()
    {
        var data = db.Books.SqlQuery(queryUnsafe).ToList();
        data.Count.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecutSqlQueryParamWithTainted_VulnerabilityIsNotReported()
    {
        var data = db.Books.SqlQuery(@"Select * from dbo.Books where title =@title", titleParam).ToList();
        data.Count.Should().Be(1);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported()
    {
        var query = GetEntityCommand();
        query.ExecuteNonQuery();
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteScalarWithTainted_VulnerabilityIsReported()
    {
        var query = GetEntityCommand();
        var result = query.ExecuteScalar();
        result.Should().NotBeNull();
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteNonQueryAsyncWithTainted_VulnerabilityIsReported()
    {
        var query = GetEntityCommand();
        _ = query.ExecuteNonQueryAsync().Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteNonQueryAsyncWithTainted_VulnerabilityIsReported2()
    {
        var query = GetEntityCommand();
        _ = query.ExecuteNonQueryAsync(CancellationToken.None).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteReaderWithTainted_VulnerabilityIsReported2()
    {
        var query = GetEntityCommand();
        query.ExecuteReader(CommandBehavior.SequentialAccess);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteReaderAsyncWithTainted_VulnerabilityIsReported2()
    {
        var query = GetEntityCommand();
        _ = query.ExecuteReaderAsync(CommandBehavior.SequentialAccess).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteReaderAsyncWithTainted_VulnerabilityIsReported3()
    {
        CommandUnsafeText += string.Empty;
        var query = GetEntityCommand();
        _ = query.ExecuteReaderAsync(CommandBehavior.SequentialAccess, CancellationToken.None).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteScalarAsyncWithTainted_VulnerabilityIsReported()
    {
        var query = GetEntityCommand();
        _ = query.ExecuteScalarAsync(CancellationToken.None).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteScalarAsyncWithTainted_VulnerabilityIsReported2()
    {
        var query = GetEntityCommand();
        _ = query.ExecuteScalarAsync().Result;
        AssertVulnerable();
    }

    private EntityCommand GetEntityCommand()
    {
        var queryString = "SELECT b.Title FROM ApplicationDbContext.Books AS b where b.Title ='" + taintedTitle + "'";
        var adapter = (IObjectContextAdapter)db;
        var objectContext = adapter.ObjectContext;
        var conn = (EntityConnection)objectContext.Connection;
        conn.Open();
        var query = new EntityCommand(queryString, conn);
        return query;
    }
}
#endif
