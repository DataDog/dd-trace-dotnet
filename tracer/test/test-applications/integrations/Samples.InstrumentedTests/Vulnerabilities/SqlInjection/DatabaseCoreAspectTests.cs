#if NET5_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class DatabaseCoreAspectTests : InstrumentationTestsBase, IDisposable
{
    protected string taintedTitle = "Think_Python";
    protected string notTaintedValue = "nottainted";
    string CommandUnsafe;
    string CommandUnsafeparameter;
    readonly string CommandSafe = "Update dbo.Books set title= title where title = @title";
    readonly string CommandSafeNoParameters = "Update dbo.Books set title= 'Think_Python' where title = 'Think_Python'";
    SqlParameter titleParam;
    string queryUnsafe;
    readonly string querySafe = "Select * from dbo.Books where title = @title";
    FormattableString formatStr;
    ApplicationDbContextCore dbContext;
    string connectionString;

    public DatabaseCoreAspectTests()
    {
        var connection = SqlDDBBCreator.Create();
        dbContext = new ApplicationDbContextCore(connection, false);
        AddTainted(taintedTitle);
        titleParam = new SqlParameter("@title", taintedTitle);
        queryUnsafe = "Select * from dbo.Books where title ='" + taintedTitle + "'";
        formatStr = $"Update dbo.Books set title= title where title = {taintedTitle}";
        CommandUnsafeparameter = "Update dbo.Books set title=title where title ='" + taintedTitle + "' or title=@title";
        CommandUnsafe = "Update dbo.Books set title= title where title ='" + taintedTitle + "'";
        dbContext.Database.OpenConnection();
    }

    public void Dispose()
    {
        dbContext.Database.CloseConnection();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawWithTainted_VulnerabilityIsReported()
    {
        dbContext.Database.ExecuteSqlRaw(CommandUnsafe);
        AssertVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawWithTainted_VulnerabilityIsReported2()
    {
        dbContext.Database.ExecuteSqlRaw(CommandUnsafe, titleParam);
        AssertVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawWithTainted_VulnerabilityIsReported3()
    {
        dbContext.Database.ExecuteSqlRaw(CommandUnsafe, new List<SqlParameter>() { titleParam });
        AssertVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawAsyncWithTainted_VulnerabilityIsReported()
    {
        dbContext.Database.ExecuteSqlRawAsync(CommandUnsafe);
        AssertVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawAsyncWithTainted_VulnerabilityIsReported2()
    {
        dbContext.Database.ExecuteSqlRawAsync(CommandUnsafeparameter, titleParam);
        AssertVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawAsyncWithTainted_VulnerabilityIsReported3()
    {
        dbContext.Database.ExecuteSqlRawAsync(CommandUnsafeparameter, new List<SqlParameter>() { titleParam });
        AssertVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawAsyncWithTainted_VulnerabilityIsReported4()
    {
        dbContext.Database.ExecuteSqlRawAsync(CommandUnsafe, CancellationToken.None);
        AssertVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawAsyncyWithTainted_VulnerabilityIsReported5()
    {
        dbContext.Database.ExecuteSqlRawAsync(CommandUnsafe, new List<SqlParameter>() { titleParam }, CancellationToken.None);
        AssertVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingFromSqlRawWithTainted_VulnerabilityIsReported()
    {
        dbContext.Books.FromSqlRaw(queryUnsafe).ToList();
        AssertVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingFromSqlRawWithTainted_VulnerabilityIsReported2()
    {
        queryUnsafe += " ";
        dbContext.Books.FromSqlRaw(queryUnsafe, titleParam).ToList();
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
    public void GivenACoreDatabase_WhenCallingFromSqlRawWithTainted_VulnerabilityIsReported3()
    {
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
    public void GivenACoreDatabase_WhenCallingExecuteNonQueryWithTainted_VulnerabilityIsReported()
    {
        var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = CommandUnsafe;
        command.ExecuteNonQuery();
        dbContext.Database.CloseConnection();
        AssertVulnerable();
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
        dbContext.Database.ExecuteSqlRaw(CommandSafeNoParameters);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawSafe_VulnerabilityIsNotReported2()
    {
        dbContext.Database.ExecuteSqlRaw(CommandSafe, titleParam);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawSafe_VulnerabilityIsNotReporte3d()
    {
        dbContext.Database.ExecuteSqlRaw(CommandSafe, new List<SqlParameter>() { titleParam });
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawAsyncSafe_VulnerabilityIsNotReported()
    {
        dbContext.Database.ExecuteSqlRawAsync(CommandSafeNoParameters);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawAsyncSafe_VulnerabilityIsNotReported2()
    {
        dbContext.Database.ExecuteSqlRawAsync(CommandSafe, titleParam);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawAsyncSafe_VulnerabilityIsNotReported3()
    {
        dbContext.Database.ExecuteSqlRawAsync(CommandSafe, new List<SqlParameter>() { titleParam });
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawAsyncSafe_VulnerabilityIsNotReporte4d()
    {
        dbContext.Database.ExecuteSqlRawAsync(CommandSafeNoParameters, CancellationToken.None);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenACoreDatabase_WhenCallingExecuteSqlRawAsyncSafe_VulnerabilityIsNotReported5()
    {
        dbContext.Database.ExecuteSqlRawAsync(CommandSafe, new List<SqlParameter>() { titleParam }, CancellationToken.None);
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
        dbContext.Books.FromSqlInterpolated($"SELECT * FROM dbo.Books where title = {titleParam}").ToList();
        AssertNotVulnerable();
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
