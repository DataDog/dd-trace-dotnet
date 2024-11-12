#if NETCOREAPP3_0_OR_GREATER

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

#if !NET6_0_OR_GREATER
[Xunit.Trait("Category", "AlpineArmUnsupported")] // sqlite isn't supported in .NET 5 on Alpine
#endif
public class EFCoreSqliteTests : EFCoreBaseTests
{
    public EFCoreSqliteTests()
    {
        var connection = MicrosoftDataSqliteDdbbCreator.Create();
        dbContext = new ApplicationDbContextCore(connection, true);
        dbContext.Database.OpenConnection();
        titleParam = new SqliteParameter("@title", taintedTitle);
    }
}

#endif
