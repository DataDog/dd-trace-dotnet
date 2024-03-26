// <copyright file="EFCoreSqliteTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETCOREAPP2_1

using System.Data;
using System.Data.Entity.Core.EntityClient;
using System.Data.Entity.Infrastructure;
using System.Data.SQLite;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;
[Trait("Category", "ArmUnsupported")]
public class EFSqliteTests : EFBaseTests
{
    public EFSqliteTests()
    {
        var connection = SqliteDDBBCreator.Create();
        db = new ApplicationDbSqlLiteContext(connection);

        if (db.Database.Connection.State != ConnectionState.Open)
        {
            db.Database.Connection.Open();
        }

        CommandUnsafeText = "Update Books set title= title where title ='" + taintedTitle + "'";
        queryUnsafe = "SELECT * from Books where title like '" + taintedTitle + "'";
        titleParam = new SQLiteParameter("@title", taintedTitle);
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecutSqlQueryWithTainted_VulnerabilityIsReported()
    {
        var data = (db as ApplicationDbSqlLiteContext).Books.SqlQuery(queryUnsafe).ToList();
        data.Count.Should().Be(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenEntityFramework_WhenCallingExecutSqlQueryParamWithTainted_VulnerabilityIsNotReported()
    {
        var data = (db as ApplicationDbSqlLiteContext).Books.SqlQuery(@"Select * from Books where title =@title", titleParam).ToList();
        data.Count.Should().Be(1);
        AssertNotVulnerable();
    }

    protected override EntityCommand GetEntityCommand(string title)
    {
        var queryString = "SELECT b.Title FROM ApplicationDbSqlLiteContext.Books AS b where b.Title ='" + title + "'";
        var adapter = (IObjectContextAdapter)db;
        var objectContext = adapter.ObjectContext;
        EntityConnection conn = (EntityConnection)objectContext.Connection;
        conn.Open();
        EntityCommand query = new EntityCommand(queryString, conn);
        return query;
    }
}

#endif
