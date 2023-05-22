#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class EFCoreSqliteTests : InstrumentationTestsBase, IDisposable
{
    protected string taintedTitle = "Think_Python";
    protected string notTaintedValue = "nottainted";
    string CommandTextUnsafe;

    SqliteParameter titleParam;
    string queryUnsafe;
    ApplicationDbContextCore dbContext;

    public EFCoreSqliteTests()
    {
        var connection = MicrosoftDataSqliteDdbbCreator.Create();
        dbContext = new ApplicationDbContextCore(connection, true);
        AddTainted(taintedTitle);
        titleParam = new SqliteParameter("@title", taintedTitle);
        queryUnsafe = "Select * from Books where title ='" + taintedTitle + "'";
        CommandTextUnsafe = "Update Books set title= title where title ='" + taintedTitle + "'";
        dbContext.Database.OpenConnection();
    }

    public void Dispose()
    {
        dbContext.Database.CloseConnection();
    }

#if NET5_0_OR_GREATER
    [Fact]
    public void GivenAMicrosoftSqliteEFCoreDatabase_WhenCallingExecuteSqlRawWithTainted_VulnerabilityIsReported()
    {
        dbContext.Database.ExecuteSqlRaw(CommandTextUnsafe);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftSqliteEFCoreDatabase_WhenCallingExecuteSqlRawWithTainted_VulnerabilityIsReported2()
    {
        dbContext.Database.ExecuteSqlRaw(CommandTextUnsafe, new List<object>());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftSqliteEFCoreDatabase_WhenCallingExecuteSqlRawWithTainted_VulnerabilityIsReported3()
    {
        dbContext.Database.ExecuteSqlRaw(CommandTextUnsafe, new object[] { });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftSqliteEFCoreDatabase_WhenCallingExecuteSqlRawAsyncWithTainted_VulnerabilityIsReported()
    {
        dbContext.Database.ExecuteSqlRawAsync(CommandTextUnsafe);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftSqliteEFCoreDatabase_WhenCallingExecuteSqlRawAsyncWithTainted_VulnerabilityIsReported2()
    {
        dbContext.Database.ExecuteSqlRawAsync(CommandTextUnsafe, CancellationToken.None);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftSqliteEFCoreDatabase_WhenCallingExecuteSqlRawAsyncWithTainted_VulnerabilityIsReported3()
    {
        dbContext.Database.ExecuteSqlRawAsync(CommandTextUnsafe, new object[] { });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftSqliteEFCoreDatabase_WhenCallingExecuteSqlRawAsyncWithTainted_VulnerabilityIsReported4()
    {
        dbContext.Database.ExecuteSqlRawAsync(CommandTextUnsafe, new List<object>());
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftSqliteEFCoreDatabase_WhenCallingExecuteSqlRawAsyncWithTainted_VulnerabilityIsReported5()
    {
        dbContext.Database.ExecuteSqlRawAsync(CommandTextUnsafe, new List<object>(), CancellationToken.None);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftSqliteEFCoreDatabase_WhenCallingFromSqlRawWithTainted_VulnerabilityIsReported()
    {
        dbContext.Books.FromSqlRaw(queryUnsafe).ToList();
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftSqliteEFCoreDatabase_WhenCallingFromSqlWithTainted_VulnerabilityIsReported2()
    {
        dbContext.Books.FromSqlRaw(queryUnsafe, titleParam).ToList();
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftSqliteEFCoreDatabase_WhenCallingFromSqlRawWithTainted_VulnerabilityIsReported3()
    {
        dbContext.Books.FromSqlRaw("Select * from dbo.Books where title ='" + taintedTitle + "'");
        AssertVulnerable();
    }
#endif

    [Fact]
    public void GivenAMicrosoftSqliteEFCoreDatabase_WhenCallingExecuteScalarWithTainted_VulnerabilityIsReported()
    {
        var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = queryUnsafe;
        command.ExecuteScalar();
        dbContext.Database.CloseConnection();
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMicrosoftSqliteEFCoreDatabase_WhenCallingToListSafe_VulnerabilityIsNotReported()
    {
        dbContext.Books.Where(x => x.Title == taintedTitle).ToList();
        AssertNotVulnerable();
    }
}

#endif
