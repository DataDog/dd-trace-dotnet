// <copyright file="SqlQueryParserTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util;

public class SqlQueryParserTests
{
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", null, null)]
    [InlineData("   ", null, null)]
    [InlineData("SELECT * FROM users WHERE id = 1", "SELECT", "users")]
    [InlineData("select * from users", "SELECT", "users")]
    [InlineData("INSERT INTO orders (id) VALUES (1)", "INSERT", "orders")]
    [InlineData("insert into orders (id) values (1)", "INSERT", "orders")]
    [InlineData("UPDATE users SET name = 'Alice' WHERE id = 1", "UPDATE", "users")]
    [InlineData("update users set name = 'Alice'", "UPDATE", "users")]
    [InlineData("DELETE FROM sessions WHERE expired = 1", "DELETE", "sessions")]
    [InlineData("delete from sessions", "DELETE", "sessions")]
    [InlineData("CREATE TABLE foo (id INT)", "CREATE", null)]
    [InlineData("DROP TABLE foo", "DROP", null)]
    [InlineData("TRUNCATE TABLE foo", "TRUNCATE", null)]
    [InlineData("EXEC sp_help", "EXEC", null)]
    [InlineData("EXECUTE sp_help", "EXECUTE", null)]
    [InlineData("UNKNOWN STATEMENT", null, null)]
    public void Parse_BasicCases(string? input, string? expectedOp, string? expectedTable)
    {
        var (op, table) = SqlQueryParser.Parse(input);
        op.Should().Be(expectedOp);
        table.Should().Be(expectedTable);
    }

    [Theory]
    [InlineData("SELECT * FROM dbo.users", "SELECT", "users")]
    [InlineData("SELECT * FROM myschema.orders", "SELECT", "orders")]
    [InlineData("INSERT INTO dbo.orders (id) VALUES (1)", "INSERT", "orders")]
    [InlineData("UPDATE dbo.users SET name = 'x'", "UPDATE", "users")]
    [InlineData("DELETE FROM mydb.myschema.sessions", "DELETE", "sessions")]
    public void Parse_SchemaQualifiedUnquoted(string input, string expectedOp, string expectedTable)
    {
        var (op, table) = SqlQueryParser.Parse(input);
        op.Should().Be(expectedOp);
        table.Should().Be(expectedTable);
    }

    [Theory]
    [InlineData("SELECT * FROM \"users\"", "SELECT", "users")]
    [InlineData("SELECT * FROM `users`", "SELECT", "users")]
    [InlineData("SELECT * FROM [users]", "SELECT", "users")]
    [InlineData("SELECT * FROM [dbo].[users]", "SELECT", "users")]
    [InlineData("SELECT * FROM \"dbo\".\"users\"", "SELECT", "users")]
    [InlineData("INSERT INTO [dbo].[orders] (id) VALUES (1)", "INSERT", "orders")]
    public void Parse_QuotedIdentifiers(string input, string expectedOp, string expectedTable)
    {
        var (op, table) = SqlQueryParser.Parse(input);
        op.Should().Be(expectedOp);
        table.Should().Be(expectedTable);
    }

    [Theory]
    [InlineData("/*dddbs=mydb,ddps=svc*/SELECT * FROM users", "SELECT", "users")]
    [InlineData("/* dbm comment */SELECT * FROM orders", "SELECT", "orders")]
    [InlineData("/* a *//* b */UPDATE items SET x=1", "UPDATE", "items")]
    public void Parse_DbmPrefixedQueries(string input, string expectedOp, string expectedTable)
    {
        var (op, table) = SqlQueryParser.Parse(input);
        op.Should().Be(expectedOp);
        table.Should().Be(expectedTable);
    }

    [Theory]
    [InlineData("SELECT * FROM a, b WHERE a.id = b.id")]
    [InlineData("SELECT * FROM (SELECT id FROM inner_table) subq")]
    public void Parse_AmbiguousSelect_ReturnsNullTable(string input)
    {
        var (op, table) = SqlQueryParser.Parse(input);
        op.Should().Be("SELECT");
        table.Should().BeNull();
    }

    [Fact]
    public void Parse_SelectWithNoFrom_ReturnsNullTable()
    {
        var (op, table) = SqlQueryParser.Parse("SELECT 1 + 1");
        op.Should().Be("SELECT");
        table.Should().BeNull();
    }

    [Theory]
    [InlineData("WITH cte AS (SELECT 1) SELECT * FROM cte")]
    [InlineData("WITH cte AS (SELECT 1) UPDATE users SET x=1")]
    [InlineData("WITH cte AS (SELECT 1) INSERT INTO orders SELECT * FROM cte")]
    public void Parse_WithCte_ReturnsNullOperation(string input)
    {
        var (op, table) = SqlQueryParser.Parse(input);
        op.Should().BeNull();
        table.Should().BeNull();
    }

    [Theory]
    [InlineData("SELECT * FROM users u JOIN orders o ON u.id=o.user_id")]
    [InlineData("SELECT * FROM users INNER JOIN orders ON users.id=orders.user_id")]
    [InlineData("SELECT * FROM users LEFT JOIN orders ON users.id=orders.user_id")]
    public void Parse_JoinQuery_ReturnsNullTable(string input)
    {
        var (op, table) = SqlQueryParser.Parse(input);
        op.Should().Be("SELECT");
        table.Should().BeNull();
    }
}
