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
        AddTainted(taintedTitle);
        formatStr = $"Update Books set title= title where title = {taintedTitle}";
        commandUnsafeparameter = "Update Books set title=title where title ='" + taintedTitle + "' or title=@title";
        commandUnsafe = "Update Books set title= title where title ='" + taintedTitle + "'";
        queryUnsafe = "Select * from Books where title ='" + taintedTitle + "'";
    }

    public void Dispose()
    {
        dbContext.Database.CloseConnection();
    }
}
#endif
