#if NETCOREAPP3_0_OR_GREATER

using System.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

// We cannot use localDB on linux and these calls cannot be mocked
[Trait("Category", "LinuxUnsupported")]
public class EFCoreTests : EFCoreBaseTests
{
    public EFCoreTests()
    {
        var connection = SqlDDBBCreator.Create();
        dbContext = new ApplicationDbContextCore(connection, false);
        dbContext.Database.OpenConnection();
        titleParam = new SqlParameter("@title", taintedTitle);
    }
}
#endif
