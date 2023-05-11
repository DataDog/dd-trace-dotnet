// <copyright file="EFCoreSqliteTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETCOREAPP2_1

using System.Data;
using System.Data.Entity;
using System.Data.Entity.Core.EntityClient;
using System.Data.Entity.Infrastructure;
using System.Data.SQLite;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;
public class EFFFSqliteTests : InstrumentationTestsBase
{
    string CommandUnsafeText;
    readonly string CommandSafe = "Update Books set title= title where title = @title";
    string queryUnsafe;
    SQLiteParameter titleParam;
    protected string taintedTitle = "CLR via C#";
    protected string taintedTitleInjection = "s' or '1' = '1";
    private ApplicationDbSqlLiteContext db;

    public EFFFSqliteTests()
    {
        var connection = SqliteDDBBCreator.CreateDatabase();
        db = new ApplicationDbSqlLiteContext(connection);

        if (db.Database.Connection.State != ConnectionState.Open)
        {
            db.Database.Connection.Open();
        }

        AddTainted(taintedTitle);
        AddTainted(taintedTitleInjection);
        CommandUnsafeText = "Update Books set title= title where title ='" + taintedTitle + "'";
        queryUnsafe = "SELECT * from Books where title like '" + taintedTitle + "'";
        titleParam = new SQLiteParameter("@title", taintedTitle);
    }

    [Fact]
    public void GivenAVulnerability_WhenGetStack_ThenLocationIsCorrect()
    {
        AssertLocation(nameof(EFFFSqliteTests));
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteSqlCommandWithTainted_VulnerabilityIsReported()
    {
        var result = db.Database.ExecuteSqlCommand(CommandUnsafeText);
        result.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteSqlCommandObjectsWithTainted_VulnerabilityIsReported()
    {
        var result = db.Database.ExecuteSqlCommand(CommandUnsafeText, titleParam);
        result.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteSqlCommandTransactionalBehaviorWithTainted_VulnerabilityIsReported()
    {
        var result = db.Database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction, CommandUnsafeText);
        result.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteSqlCommandTransactionalBehaviorObjectWithTainted_VulnerabilityIsReported()
    {
        var result = db.Database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction, CommandUnsafeText, titleParam);
        result.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteSqlCommandTransactionalBehaviorObjectSafe_VulnerabilityIsNotReported()
    {
        var result = db.Database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction, CommandSafe, titleParam);
        result.Should().Be(1);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteSqlCommandObjectSafe_VulnerabilityIsNotReported()
    {
        var result = db.Database.ExecuteSqlCommand(CommandSafe, titleParam);
        result.Should().Be(1);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteSqlCommandParamSafe_VulnerabilityIsNotReported()
    {
        var result = db.Database.ExecuteSqlCommand(@"Update Books set title=@title where title =@title", titleParam);
        result.Should().Be(1);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteSqlCommandAsyncWithTainted_VulnerabilityIsReported()
    {
        var result = db.Database.ExecuteSqlCommandAsync(CommandUnsafeText).Result;
        result.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteSqlCommandAsyncWithTainted_VulnerabilityIsReported2()
    {
        var result = db.Database.ExecuteSqlCommandAsync(CommandUnsafeText, titleParam).Result;
        result.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteSqlCommandAsyncWithTainted_VulnerabilityIsReported3()
    {
        var result = db.Database.ExecuteSqlCommandAsync(TransactionalBehavior.DoNotEnsureTransaction, CommandUnsafeText).Result;
        result.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteSqlCommandAsyncWithTainted_VulnerabilityIsReported4()
    {
        var result = db.Database.ExecuteSqlCommandAsync(TransactionalBehavior.DoNotEnsureTransaction, CommandUnsafeText, titleParam).Result;
        result.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteSqlCommandAsyncWithTainted_VulnerabilityIsReported5()
    {
        var result = db.Database.ExecuteSqlCommandAsync(TransactionalBehavior.DoNotEnsureTransaction, CommandUnsafeText, CancellationToken.None, titleParam).Result;
        result.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteSqlCommandAsyncWithTainted_VulnerabilityIsReported6()
    {
        var result = db.Database.ExecuteSqlCommandAsync(CommandUnsafeText, CancellationToken.None, titleParam).Result;
        result.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteSqlCommandAsyncCancellationTokenSafe_VulnerabilityIsNotReported6()
    {
        var result = db.Database.ExecuteSqlCommandAsync(CommandSafe, CancellationToken.None, titleParam).Result;
        result.Should().Be(1);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteSqlCommandAsyncSafe_VulnerabilityIsNotReported6()
    {
        var result = db.Database.ExecuteSqlCommandAsync(CommandSafe, titleParam).Result;
        result.Should().Be(1);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecutSqlQueryGenericWithTainted_VulnerabilityIsReported()
    {
        var data = db.Database.SqlQuery<Book>(queryUnsafe).ToList();
        data.Count.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecutSqlQueryGenericParamWithTainted_VulnerabilityIsNotReported()
    {
        var data = db.Database.SqlQuery<Book>(@"Select * from Books where title =@title", titleParam).ToList();
        data.Count.Should().Be(1);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecutSqlQueryWithTainted_VulnerabilityIsReported()
    {
        var data = db.Books.SqlQuery(queryUnsafe).ToList();
        data.Count.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecutSqlQueryParamWithTainted_VulnerabilityIsNotReported()
    {
        var data = db.Books.SqlQuery(@"Select * from Books where title =@title", titleParam).ToList();
        data.Count.Should().Be(1);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported()
    {
        EntityCommand query = GetEntityCommand();
        query.ExecuteNonQuery();
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteScalarWithTainted_VulnerabilityIsReported()
    {
        EntityCommand query = GetEntityCommand();
        var result = query.ExecuteScalar();
        result.Should().NotBeNull();
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteNonQueryAsyncWithTainted_VulnerabilityIsReported()
    {
        EntityCommand query = GetEntityCommand();
        _ = query.ExecuteNonQueryAsync().Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteNonQueryAsyncWithTainted_VulnerabilityIsReported2()
    {
        EntityCommand query = GetEntityCommand();
        _ = query.ExecuteNonQueryAsync(CancellationToken.None).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteReaderWithTainted_VulnerabilityIsReported2()
    {
        EntityCommand query = GetEntityCommand(taintedTitleInjection);
        var result = query.ExecuteReader(CommandBehavior.SequentialAccess);
        int rowCount = 0;

        while (result.Read())
        {
            rowCount++;
        }
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteReaderAsyncWithTainted_VulnerabilityIsReported2()
    {
        EntityCommand query = GetEntityCommand();
        _ = query.ExecuteReaderAsync(CommandBehavior.SequentialAccess).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteReaderAsyncWithTainted_VulnerabilityIsReported3()
    {
        EntityCommand query = GetEntityCommand();
        _ = query.ExecuteReaderAsync(CommandBehavior.SequentialAccess, CancellationToken.None).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteScalarAsyncWithTainted_VulnerabilityIsReported()
    {
        EntityCommand query = GetEntityCommand();
        _ = query.ExecuteScalarAsync(CancellationToken.None).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFrameworkSqlite_WhenCallingExecuteScalarAsyncWithTainted_VulnerabilityIsReported2()
    {
        EntityCommand query = GetEntityCommand();
        _ = query.ExecuteScalarAsync().Result;
        AssertVulnerable();
    }

    private EntityCommand GetEntityCommand(string title = null)
    {
        var queryString = string.Empty;

        if (title == null)
        {
            queryString = "SELECT b.Title FROM ApplicationDbSqlLiteContext.Books AS b where b.Title ='" + taintedTitle + "'";
        }
        else
        {
            queryString = "SELECT b.Title FROM ApplicationDbSqlLiteContext.Books AS b where b.Title ='" + title + "'";
        }
            var adapter = (IObjectContextAdapter)db;
        var objectContext = adapter.ObjectContext;
        EntityConnection conn = (EntityConnection)objectContext.Connection;
        conn.Open();
        EntityCommand query = new EntityCommand(queryString, conn);
        return query;
    }
}

#endif
