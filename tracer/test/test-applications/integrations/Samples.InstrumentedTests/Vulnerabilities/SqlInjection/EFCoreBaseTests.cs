#if NETCOREAPP3_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public abstract class EFCoreBaseTests: InstrumentationTestsBase, IDisposable
{
    protected string taintedTitle = "Think_Python";
    protected string notTaintedValue = "nottainted";
    protected string commandUnsafe;
    protected string commandUnsafeparameter;
    protected readonly string commandSafe = "Update Books set title= title where title = @title";
    protected readonly string commandSafeNoParameters = "Update Books set title= 'Think_Python' where title = 'Think_Python'";
    protected readonly string querySafe = "Select * from Books where title = @title";
    protected DbParameter titleParam;
    protected string queryUnsafe;
    protected FormattableString formatStr;
    protected ApplicationDbContextCore dbContext;

    public EFCoreBaseTests()
    {
        PrepareData();
    }

    private void PrepareData()
    {
        AddTainted(taintedTitle);
        formatStr = $"Update Books set title= title where title = {taintedTitle}";
        commandUnsafeparameter = "Update Books set title=title where title ='" + taintedTitle + "' or title=@title";
        commandUnsafe = "Update Books set title= title where title ='" + taintedTitle + "'";
        queryUnsafe = "Select * from Books where title ='" + taintedTitle + "'";
    }

    public override void Dispose()
    {
        dbContext.Database.CloseConnection();
        base.Dispose();
    }

#if NET5_0_OR_GREATER

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawWithTainted_VulnerabilityIsReported()
    {
        dbContext.Database.ExecuteSqlRaw(commandUnsafe);
        AssertVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawWithTainted_VulnerabilityIsReported2()
    {
        dbContext.Database.ExecuteSqlRaw(commandUnsafe, titleParam);
        AssertVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawWithTainted_VulnerabilityIsReported3()
    {
        dbContext.Database.ExecuteSqlRaw(commandUnsafe, new List<DbParameter>() { titleParam });
        AssertVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawAsyncWithTainted_VulnerabilityIsReported()
    {
        dbContext.Database.ExecuteSqlRawAsync(commandUnsafe);
        AssertVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawAsyncWithTainted_VulnerabilityIsReported2()
    {
        dbContext.Database.ExecuteSqlRawAsync(commandUnsafeparameter, titleParam);
        AssertVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawAsyncWithTainted_VulnerabilityIsReported3()
    {
        dbContext.Database.ExecuteSqlRawAsync(commandUnsafeparameter, new List<DbParameter>() { titleParam });
        AssertVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawAsyncWithTainted_VulnerabilityIsReported4()
    {
        dbContext.Database.ExecuteSqlRawAsync(commandUnsafe, CancellationToken.None);
        AssertVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawAsyncyWithTainted_VulnerabilityIsReported5()
    {
        dbContext.Database.ExecuteSqlRawAsync(commandUnsafe, new List<DbParameter>() { titleParam }, CancellationToken.None);
        AssertVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingFromSqlRawWithTainted_VulnerabilityIsReported()
    {
        PrepareData();
        dbContext.Books.FromSqlRaw(queryUnsafe).ToList();
        AssertVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingFromSqlRawWithTainted_VulnerabilityIsReported2()
    {
        PrepareData();
        dbContext.Books.FromSqlRaw(queryUnsafe, titleParam).ToList();
        AssertVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingFromSqlRawWithTainted_VulnerabilityIsReported3()
    {
        PrepareData();
        dbContext.Books.FromSqlRaw("Select * from dbo.Books where title ='" + taintedTitle + "'");
        AssertVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingFromSqlRawWithTaintedSecure_VulnerabilityIsNotReported2()
    {
        dbContext.Books.FromSqlRaw(@"Select * from dbo.Books where title =Title", taintedTitle);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingFromSqlInterpolatedWithTaintedSecure_VulnerabilityIsNotReported()
    {
        dbContext.Books.FromSqlInterpolated($"Select * from dbo.Books ({taintedTitle}");
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlInterpolatedSafe_VulnerabilityIsNotReported()
    {
        dbContext.Database.ExecuteSqlInterpolated(formatStr);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlInterpolatedSafe_VulnerabilityIsNotReported2()
    {
        dbContext.Database.ExecuteSqlInterpolatedAsync(formatStr);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlInterpolatedSafe_VulnerabilityIsNotReported3()
    {
        dbContext.Database.ExecuteSqlInterpolatedAsync(formatStr, CancellationToken.None);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawSafe_VulnerabilityIsNotReported()
    {
        dbContext.Database.ExecuteSqlRaw(commandSafeNoParameters);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawSafe_VulnerabilityIsNotReported2()
    {
        dbContext.Database.ExecuteSqlRaw(commandSafe, titleParam);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawSafe_VulnerabilityIsNotReporte3d()
    {
        dbContext.Database.ExecuteSqlRaw(commandSafe, new List<DbParameter>() { titleParam });
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawAsyncSafe_VulnerabilityIsNotReported()
    {
        dbContext.Database.ExecuteSqlRawAsync(commandSafeNoParameters);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawAsyncSafe_VulnerabilityIsNotReported2()
    {
        dbContext.Database.ExecuteSqlRawAsync(commandSafe, titleParam);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawAsyncSafe_VulnerabilityIsNotReported3()
    {
        dbContext.Database.ExecuteSqlRawAsync(commandSafe, new List<DbParameter>() { titleParam });
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawAsyncSafe_VulnerabilityIsNotReporte4d()
    {
        dbContext.Database.ExecuteSqlRawAsync(commandSafeNoParameters, CancellationToken.None);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawAsyncSafe_VulnerabilityIsNotReported5()
    {
        dbContext.Database.ExecuteSqlRawAsync(commandSafe, new List<DbParameter>() { titleParam }, CancellationToken.None);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingFromSqlRawSafe_VulnerabilityIsNotReported()
    {
        dbContext.Books.FromSqlRaw(querySafe, titleParam).ToList();
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingFromSqlInterpolatedSafe_VulnerabilityIsNotReported()
    {
        dbContext.Books.FromSqlInterpolated($"SELECT * FROM Books where title = {titleParam}").ToList();
        AssertNotVulnerable();
    }

#endif

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported()
    {
        var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = commandUnsafe;
        command.ExecuteNonQuery();
        dbContext.Database.CloseConnection();
        AssertVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteScalarWithTainted_VulnerabilityIsReported()
    {
        var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = queryUnsafe;
        command.ExecuteScalar();
        dbContext.Database.CloseConnection();
        AssertVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingToListSafe_VulnerabilityIsNotReported()
    {
        dbContext.Books.Where(x => x.Title == taintedTitle).ToList();
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingFirstOrDefaultSafe_VulnerabilityIsNotReported()
    {
        new List<Book>() { dbContext.Books.FirstOrDefault(x => x.Title == taintedTitle) };
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingLikeSafe_VulnerabilityIsNotReported()
    {
        (from c in dbContext.Books where EF.Functions.Like(c.Title, taintedTitle) select c).ToList();
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingToListSafe_VulnerabilityIsNotReported2()
    {
        (from c in dbContext.Books where c.Title == taintedTitle select c).ToList();
        AssertNotVulnerable();
    }
}
#endif
