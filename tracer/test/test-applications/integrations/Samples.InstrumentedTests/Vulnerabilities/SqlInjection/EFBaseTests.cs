#if !NETCOREAPP2_1
using System;
using System.Data;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Core.EntityClient;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public abstract class EFBaseTests : InstrumentationTestsBase, IDisposable
{
    protected string taintedTitle = "CLR via C#";
    protected string notTaintedValue = "nottainted";
    protected string CommandUnsafeText;
    protected readonly string CommandSafe = "Update Books set title= title where title = @title";
    protected DbParameter titleParam;
    protected string queryUnsafe;
    protected DbContext db;
    protected string taintedTitleInjection = "s' or '1' = '1";

    public EFBaseTests()
    {
        AddTainted(taintedTitle);
        AddTainted(taintedTitleInjection);
        CommandUnsafeText = "Update Books set title= title where title ='" + taintedTitle + "'";
        queryUnsafe = "SELECT * from Books where title like '" + taintedTitle + "'";
    }

    public override void Dispose()
    {
        db.Database.Connection.Close();
        db.Dispose();
        db = null;
        base.Dispose();
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
        var result = db.Database.ExecuteSqlCommand(@"Update Books set title=@title where title =@title", titleParam);
        result.Should().Be(1);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteSqlCommandFormatUnsafe_VulnerabilityIsReported()
    {
        var result = db.Database.ExecuteSqlCommand("Update Books set title='" + taintedTitle + "' where title = '" + taintedTitle + "'");
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
        _ = db.Database.SqlQuery<Book>(queryUnsafe + @" and title =@title", titleParam).ToList();
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecutSqlQueryGenericParamWithTainted_VulnerabilityIsNotReported2()
    {
        var data = db.Database.SqlQuery<Book>(@"Select * from Books where title =@title", titleParam).ToList();
        data.Count.Should().Be(1);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported()
    {
        GetEntityCommand(taintedTitle).ExecuteNonQuery();
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteScalarWithTainted_VulnerabilityIsReported()
    {
        var result = GetEntityCommand(taintedTitle).ExecuteScalar();
        result.Should().NotBeNull();
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteNonQueryAsyncWithTainted_VulnerabilityIsReported()
    {
        _ = GetEntityCommand(taintedTitle).ExecuteNonQueryAsync().Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteNonQueryAsyncWithTainted_VulnerabilityIsReported2()
    {
        _ = GetEntityCommand(taintedTitle).ExecuteNonQueryAsync(CancellationToken.None).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteReaderWithTainted_VulnerabilityIsReported2()
    {
        GetEntityCommand(taintedTitle).ExecuteReader(CommandBehavior.SequentialAccess);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteReaderAsyncWithTainted_VulnerabilityIsReported2()
    {
        _ = GetEntityCommand(taintedTitle).ExecuteReaderAsync(CommandBehavior.SequentialAccess).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteReaderAsyncWithTainted_VulnerabilityIsReported3()
    {
        _ = GetEntityCommand(taintedTitle).ExecuteReaderAsync(CommandBehavior.SequentialAccess, CancellationToken.None).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteReaderWithTainted_VulnerabilityIsReported4()
    {
        var result = GetEntityCommand(taintedTitleInjection).ExecuteReader(CommandBehavior.SequentialAccess);
        int rowCount = 0;

        while (result.Read())
        {
            rowCount++;
        }
        rowCount.Should().Be(2);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteScalarAsyncWithTainted_VulnerabilityIsReported()
    {
        _ = GetEntityCommand(taintedTitle).ExecuteScalarAsync(CancellationToken.None).Result;
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecuteScalarAsyncWithTainted_VulnerabilityIsReported2()
    {
        _ = GetEntityCommand(taintedTitle).ExecuteScalarAsync().Result;
        AssertVulnerable();
    }

    protected abstract EntityCommand GetEntityCommand(string title);
}
#endif
