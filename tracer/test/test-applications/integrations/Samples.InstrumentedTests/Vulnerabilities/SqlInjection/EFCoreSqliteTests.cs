#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

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
}

#endif
