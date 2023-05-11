#if NETCOREAPP3_0_OR_GREATER

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class EFCoreSqliteTests : EFCoreBaseTests
{ 
    public EFCoreSqliteTests()
    {
        var connection = MicrosoftDataSqliteDdbbCreator.Create();
        dbContext = new ApplicationDbContextCore(connection, true);
        dbContext.Database.OpenConnection();
        titleParam = new SqliteParameter("@title", taintedTitle);
    }

        AddTainted(taintedTitle);
        AddTainted(taintedTitleInjection);
        CommandUnsafeText = "Update Books set title= title where title ='" + taintedTitle + "'";
        queryUnsafe = "SELECT * from Books where title like '" + taintedTitle + "'";
        titleParam = new SQLiteParameter("@title", taintedTitle);
    }

    [Fact]
    public void GivenAProcess_WhenStartTaintedProcess_ThenLocationIsCorrect()
    {
        AssertLocation(nameof(EFCoreSqliteTests));
    }

#endif
