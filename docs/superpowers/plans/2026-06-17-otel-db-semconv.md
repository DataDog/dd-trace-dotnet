# OTel Database Semantic Conventions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When `DD_TRACE_OTEL_SEMANTICS_ENABLED=true`, ADO.NET SQL spans emit OTel DB semantic convention attribute names instead of Datadog legacy names, satisfying the system-tests `OTEL_SEMANTICS_DB` scenario.

**Architecture:** New `SqlQueryParser` extracts SQL operation and table from `CommandText`. New `DbOtelHelper` sets the 7 OTel attributes on a span. `DbScopeFactory` branches on `OpenTelemetrySemanticsEnabled` to call `DbOtelHelper` instead of setting legacy `SqlTags` properties. `DbCommandCache.TagsCacheItem` gains a `Port` field for `server.port`.

**Tech Stack:** C# / .NET, xUnit + FluentAssertions, Verify (snapshot testing)

## Global Constraints

- Target frameworks: `net461` through `net8.0` — avoid APIs unavailable below .NET 4.6.1 (no `ValueTuple` syntax, no `string.IsNullOrEmpty` — use `StringUtil.IsNullOrEmpty`)
- No manually edited `.g.` files
- StyleCop enforced — address warnings before committing
- Spec: `docs/superpowers/specs/2026-06-17-otel-db-semconv-design.md`
- `db.system.name` stable identifiers: `postgresql`, `microsoft.sql_server`, `mysql`, `oracle.db`, `sqlite`
- OTel mode is mutually exclusive with legacy names: when enabled, only OTel names emitted
- `server.port` emitted as a string tag (consistent with HTTP `server.port` in `HttpOtelHelper`)

---

### Task 1: SqlQueryParser

**Files:**
- Create: `tracer/src/Datadog.Trace/Util/SqlQueryParser.cs`
- Create: `tracer/test/Datadog.Trace.Tests/Util/SqlQueryParserTests.cs`

**Interfaces:**
- Produces: `internal static class SqlQueryParser` with `static (string? Operation, string? Table) Parse(string? commandText)`

- [ ] **Step 1: Create the test file with failing tests**

Create `tracer/test/Datadog.Trace.Tests/Util/SqlQueryParserTests.cs`:

```csharp
// <copyright file="SqlQueryParserTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
    [InlineData("WITH cte AS (SELECT 1) SELECT * FROM cte")]
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
}
```

- [ ] **Step 2: Run tests to confirm they fail**

```
cd /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3/tracer
dotnet test test/Datadog.Trace.Tests/Datadog.Trace.Tests.csproj -f net8.0 --filter "FullyQualifiedName~SqlQueryParserTests" --no-build 2>&1 | tail -20
```

Expected: build error — `SqlQueryParser` does not exist.

- [ ] **Step 3: Create `SqlQueryParser.cs`**

Create `tracer/src/Datadog.Trace/Util/SqlQueryParser.cs`:

```csharp
// <copyright file="SqlQueryParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Util
{
    internal static class SqlQueryParser
    {
        internal static (string? Operation, string? Table) Parse(string? commandText)
        {
            if (StringUtil.IsNullOrEmpty(commandText))
            {
                return (null, null);
            }

            var pos = SkipLeadingBlockComments(commandText, 0);
            var verb = ReadToken(commandText, pos, out pos);
            if (verb is null)
            {
                return (null, null);
            }

            var operation = verb.ToUpperInvariant() switch
            {
                "SELECT" => "SELECT",
                "INSERT" => "INSERT",
                "UPDATE" => "UPDATE",
                "DELETE" => "DELETE",
                "CREATE" => "CREATE",
                "DROP" => "DROP",
                "ALTER" => "ALTER",
                "MERGE" => "MERGE",
                "CALL" => "CALL",
                "EXEC" => "EXEC",
                "EXECUTE" => "EXECUTE",
                "TRUNCATE" => "TRUNCATE",
                _ => null
            };

            if (operation is null)
            {
                return (null, null);
            }

            var table = ExtractTable(commandText, operation);
            return (operation, table);
        }

        private static string? ExtractTable(string text, string operation)
        {
            switch (operation)
            {
                case "SELECT":
                case "DELETE":
                    return ExtractTableAfterFrom(text);
                case "INSERT":
                    return ExtractTableAfterInto(text);
                case "UPDATE":
                    return ExtractTableAfterUpdate(text);
                default:
                    return null;
            }
        }

        private static string? ExtractTableAfterFrom(string text)
        {
            var pos = 0;
            while (true)
            {
                var token = ReadToken(text, pos, out var nextPos);
                if (token is null)
                {
                    return null;
                }

                if (token.Equals("FROM", StringComparison.OrdinalIgnoreCase))
                {
                    pos = nextPos;
                    break;
                }

                pos = nextPos;
            }

            // subquery check
            var peek = SkipWhitespace(text, pos);
            if (peek < text.Length && text[peek] == '(')
            {
                return null;
            }

            var tableToken = ReadTableIdentifier(text, pos, out var afterTable);
            if (tableToken is null)
            {
                return null;
            }

            // multi-table check: comma after table
            var afterPeek = SkipWhitespace(text, afterTable);
            if (afterPeek < text.Length && text[afterPeek] == ',')
            {
                return null;
            }

            return NormalizeIdentifier(tableToken);
        }

        private static string? ExtractTableAfterInto(string text)
        {
            var pos = 0;
            while (true)
            {
                var token = ReadToken(text, pos, out var nextPos);
                if (token is null)
                {
                    return null;
                }

                if (token.Equals("INTO", StringComparison.OrdinalIgnoreCase))
                {
                    pos = nextPos;
                    break;
                }

                pos = nextPos;
            }

            var tableToken = ReadTableIdentifier(text, pos, out _);
            return tableToken is null ? null : NormalizeIdentifier(tableToken);
        }

        private static string? ExtractTableAfterUpdate(string text)
        {
            // skip UPDATE keyword
            ReadToken(text, 0, out var pos);
            var tableToken = ReadTableIdentifier(text, pos, out _);
            return tableToken is null ? null : NormalizeIdentifier(tableToken);
        }

        private static string? ReadTableIdentifier(string text, int pos, out int nextPos)
        {
            var lastToken = ReadToken(text, pos, out pos);
            if (lastToken is null)
            {
                nextPos = pos;
                return null;
            }

            // Handle [schema].[table] or "schema"."table": if next non-whitespace is '.', keep advancing
            while (true)
            {
                var peek = SkipWhitespace(text, pos);
                if (peek >= text.Length || text[peek] != '.')
                {
                    break;
                }

                var next = ReadToken(text, peek + 1, out var afterNext);
                if (next is null)
                {
                    break;
                }

                lastToken = next;
                pos = afterNext;
            }

            nextPos = pos;
            return lastToken;
        }

        private static string? NormalizeIdentifier(string token)
        {
            if (StringUtil.IsNullOrEmpty(token))
            {
                return null;
            }

            // Strip surrounding quote characters: "...", `...`, [...]
            if (token.Length >= 2)
            {
                char first = token[0];
                char last = token[token.Length - 1];
                if ((first == '"' && last == '"') ||
                    (first == '`' && last == '`') ||
                    (first == '[' && last == ']'))
                {
                    token = token.Substring(1, token.Length - 2);
                }
            }

            // Strip schema prefix from unquoted names: schema.table → table
            var dotIndex = token.LastIndexOf('.');
            if (dotIndex >= 0 && dotIndex < token.Length - 1)
            {
                token = token.Substring(dotIndex + 1);
            }

            return StringUtil.IsNullOrEmpty(token) ? null : token;
        }

        private static int SkipLeadingBlockComments(string text, int pos)
        {
            while (true)
            {
                pos = SkipWhitespace(text, pos);
                if (pos + 1 < text.Length && text[pos] == '/' && text[pos + 1] == '*')
                {
                    var end = text.IndexOf("*/", pos + 2, StringComparison.Ordinal);
                    if (end < 0)
                    {
                        return text.Length;
                    }

                    pos = end + 2;
                }
                else
                {
                    break;
                }
            }

            return pos;
        }

        private static int SkipWhitespace(string text, int pos)
        {
            while (pos < text.Length && char.IsWhiteSpace(text[pos]))
            {
                pos++;
            }

            return pos;
        }

        private static string? ReadToken(string text, int pos, out int nextPos)
        {
            pos = SkipWhitespace(text, pos);
            if (pos >= text.Length)
            {
                nextPos = pos;
                return null;
            }

            char first = text[pos];

            // Quoted identifier: read until closing quote
            char? closingQuote = first switch
            {
                '"' => '"',
                '`' => '`',
                '[' => ']',
                _ => null
            };

            if (closingQuote.HasValue)
            {
                var end = text.IndexOf(closingQuote.Value, pos + 1);
                if (end < 0)
                {
                    end = text.Length - 1;
                }

                nextPos = end + 1;
                return text.Substring(pos, nextPos - pos);
            }

            // Regular token: read until whitespace or delimiter
            var start = pos;
            while (pos < text.Length &&
                   !char.IsWhiteSpace(text[pos]) &&
                   text[pos] != ',' &&
                   text[pos] != ';' &&
                   text[pos] != '(' &&
                   text[pos] != ')')
            {
                pos++;
            }

            nextPos = pos;
            var token = text.Substring(start, pos - start);
            return StringUtil.IsNullOrEmpty(token) ? null : token;
        }
    }
}
```

- [ ] **Step 4: Build and run tests**

```
cd /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3/tracer
dotnet build src/Datadog.Trace/Datadog.Trace.csproj -f net8.0 -c Debug 2>&1 | tail -10
dotnet test test/Datadog.Trace.Tests/Datadog.Trace.Tests.csproj -f net8.0 --filter "FullyQualifiedName~SqlQueryParserTests" --no-build 2>&1 | tail -20
```

Expected: All tests pass.

- [ ] **Step 5: Commit**

```
git -C /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3 add \
  tracer/src/Datadog.Trace/Util/SqlQueryParser.cs \
  tracer/test/Datadog.Trace.Tests/Util/SqlQueryParserTests.cs
git -C /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3 commit -m "Add SqlQueryParser for OTel DB semconv operation and table extraction"
```

---

### Task 2: Add Port to DbCommandCache

**Files:**
- Modify: `tracer/src/Datadog.Trace/Util/DbCommandCache.cs`

**Interfaces:**
- Consumes: nothing new
- Produces: `TagsCacheItem.Port` (nullable string) — consumed by Task 3 (`DbOtelHelper`) and Task 4 (`DbScopeFactory`)

- [ ] **Step 1: Extend `TagsCacheItem` and `ExtractTagsFromConnectionString`**

In `tracer/src/Datadog.Trace/Util/DbCommandCache.cs`, make these changes:

1. Add `Port` field to `TagsCacheItem`:

```csharp
internal readonly struct TagsCacheItem
{
    public readonly string? DbName;
    public readonly string? DbUser;
    public readonly string? OutHost;
    public readonly string? Port;      // new

    public TagsCacheItem(string? dbName, string? dbUser, string? outHost, string? port)
    {
        DbName = dbName;
        DbUser = dbUser;
        OutHost = outHost;
        Port = port;
    }
}
```

2. Update `ExtractTagsFromConnectionString` to extract port (add `port:` arg):

```csharp
private static TagsCacheItem ExtractTagsFromConnectionString(string connectionString)
{
    try
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

        return new TagsCacheItem(
            dbName: GetConnectionStringValue(builder, "Database", "Initial Catalog", "InitialCatalog"),
            dbUser: GetConnectionStringValue(builder, "User ID", "UserID", "User", "Uid", "Username", "User Name"),
            outHost: GetConnectionStringValue(builder, "Server", "Data Source", "DataSource", "Network Address", "NetworkAddress", "Address", "Addr", "Host", "Hostname", "Host Name"),
            port: GetConnectionStringValue(builder, "Port", "Port Number"));
    }
    catch (Exception)
    {
        return default;
    }
}
```

- [ ] **Step 2: Fix the existing `TagsCacheItem` construction call**

The old constructor takes 3 args. Find all callsites (there is one other besides `ExtractTagsFromConnectionString` — in `DbCommandCache.GetTagsFromDbCommand` via `return default` which is fine since `default` zero-inits). The only explicit construction is in `ExtractTagsFromConnectionString` — already updated above.

Confirm no other `new TagsCacheItem(` calls exist:

```
grep -rn "new TagsCacheItem(" /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3/tracer/src/ --include="*.cs"
```

- [ ] **Step 3: Build to verify**

```
cd /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3/tracer
dotnet build src/Datadog.Trace/Datadog.Trace.csproj -f net8.0 -c Debug 2>&1 | tail -10
```

Expected: Build succeeds with no errors.

- [ ] **Step 4: Commit**

```
git -C /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3 add \
  tracer/src/Datadog.Trace/Util/DbCommandCache.cs
git -C /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3 commit -m "Add Port field to DbCommandCache.TagsCacheItem"
```

---

### Task 3: DbOtelHelper

**Files:**
- Create: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AdoNet/DbOtelHelper.cs`
- Create: `tracer/test/Datadog.Trace.Tests/ClrProfiler/AutoInstrumentation/AdoNet/DbOtelHelperTests.cs`

**Interfaces:**
- Consumes: `SqlQueryParser.Parse(string?)`, `TagsCacheItem.Port`, `Tags.PeerService`
- Produces: `internal static class DbOtelHelper` with `static void SetDatabaseAttributes(ISpan span, string dbType, string? dbName, string? outHost, string? port, string commandText, bool peerServiceEnabled)`

- [ ] **Step 1: Create the test file**

Create `tracer/test/Datadog.Trace.Tests/ClrProfiler/AutoInstrumentation/AdoNet/DbOtelHelperTests.cs`:

```csharp
// <copyright file="DbOtelHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet;
using Datadog.Trace.Tests.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.AdoNet;

public class DbOtelHelperTests
{
    // ── db.system.name mapping ────────────────────────────────────────────────────

    [Theory]
    [InlineData("postgres",   "postgresql")]
    [InlineData("sql-server", "microsoft.sql_server")]
    [InlineData("mysql",      "mysql")]
    [InlineData("oracle",     "oracle.db")]
    [InlineData("sqlite",     "sqlite")]
    [InlineData("someother",  "someother")]   // passthrough
    public void SetDatabaseAttributes_MapsDbSystemName(string dbType, string expectedSystemName)
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, dbType, dbName: null, outHost: null, port: null, commandText: string.Empty, peerServiceEnabled: false);

        span.GetTag("db.system.name").Should().Be(expectedSystemName);
    }

    // ── db.namespace ──────────────────────────────────────────────────────────────

    [Fact]
    public void SetDatabaseAttributes_SetsDbNamespace_WhenDbNameProvided()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: "mydb", outHost: null, port: null, commandText: string.Empty, peerServiceEnabled: false);

        span.GetTag("db.namespace").Should().Be("mydb");
    }

    [Fact]
    public void SetDatabaseAttributes_OmitsDbNamespace_WhenDbNameNull()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: null, outHost: null, port: null, commandText: string.Empty, peerServiceEnabled: false);

        span.GetTag("db.namespace").Should().BeNull();
    }

    // ── server.address ────────────────────────────────────────────────────────────

    [Fact]
    public void SetDatabaseAttributes_SetsServerAddress_WhenOutHostProvided()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: null, outHost: "db.host.local", port: null, commandText: string.Empty, peerServiceEnabled: false);

        span.GetTag("server.address").Should().Be("db.host.local");
    }

    [Fact]
    public void SetDatabaseAttributes_OmitsServerAddress_WhenOutHostNull()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: null, outHost: null, port: null, commandText: string.Empty, peerServiceEnabled: false);

        span.GetTag("server.address").Should().BeNull();
    }

    // ── server.port ───────────────────────────────────────────────────────────────

    [Fact]
    public void SetDatabaseAttributes_SetsServerPort_WhenPortProvided()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: null, outHost: null, port: "5432", commandText: string.Empty, peerServiceEnabled: false);

        span.GetTag("server.port").Should().Be("5432");
    }

    [Fact]
    public void SetDatabaseAttributes_OmitsServerPort_WhenPortNull()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: null, outHost: null, port: null, commandText: string.Empty, peerServiceEnabled: false);

        span.GetTag("server.port").Should().BeNull();
    }

    // ── db.query.text ─────────────────────────────────────────────────────────────

    [Fact]
    public void SetDatabaseAttributes_SetsDbQueryText_WhenCommandTextProvided()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: null, outHost: null, port: null, commandText: "SELECT 1", peerServiceEnabled: false);

        span.GetTag("db.query.text").Should().Be("SELECT 1");
    }

    [Fact]
    public void SetDatabaseAttributes_OmitsDbQueryText_WhenCommandTextEmpty()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: null, outHost: null, port: null, commandText: string.Empty, peerServiceEnabled: false);

        span.GetTag("db.query.text").Should().BeNull();
    }

    // ── db.operation.name + db.collection.name ────────────────────────────────────

    [Fact]
    public void SetDatabaseAttributes_SetsOperationAndCollection_FromParsedSql()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: null, outHost: null, port: null, commandText: "SELECT * FROM orders WHERE id = 1", peerServiceEnabled: false);

        span.GetTag("db.operation.name").Should().Be("SELECT");
        span.GetTag("db.collection.name").Should().Be("orders");
    }

    [Fact]
    public void SetDatabaseAttributes_OmitsCollection_WhenAmbiguous()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: null, outHost: null, port: null, commandText: "SELECT * FROM a, b", peerServiceEnabled: false);

        span.GetTag("db.operation.name").Should().Be("SELECT");
        span.GetTag("db.collection.name").Should().BeNull();
    }

    // ── peer.service ──────────────────────────────────────────────────────────────

    [Fact]
    public void SetDatabaseAttributes_SetsPeerService_WhenPeerServiceEnabled_PrefersDbName()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: "mydb", outHost: "myhost", port: null, commandText: string.Empty, peerServiceEnabled: true);

        span.GetTag("peer.service").Should().Be("mydb");
    }

    [Fact]
    public void SetDatabaseAttributes_SetsPeerService_WhenPeerServiceEnabled_FallsBackToOutHost()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: null, outHost: "myhost", port: null, commandText: string.Empty, peerServiceEnabled: true);

        span.GetTag("peer.service").Should().Be("myhost");
    }

    [Fact]
    public void SetDatabaseAttributes_OmitsPeerService_WhenPeerServiceDisabled()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: "mydb", outHost: "myhost", port: null, commandText: string.Empty, peerServiceEnabled: false);

        span.GetTag("peer.service").Should().BeNull();
    }

    // ── legacy names absent ───────────────────────────────────────────────────────

    [Fact]
    public void SetDatabaseAttributes_DoesNotSetLegacyTagNames()
    {
        var span = SpanFactory.CreateSpan();
        DbOtelHelper.SetDatabaseAttributes(span, "postgres", dbName: "mydb", outHost: "myhost", port: "5432", commandText: "SELECT 1", peerServiceEnabled: false);

        span.GetTag("db.type").Should().BeNull();
        span.GetTag("db.name").Should().BeNull();
        span.GetTag("out.host").Should().BeNull();
        span.GetTag("out.port").Should().BeNull();
    }
}
```

`SpanFactory.CreateSpan()` — add this helper to `tracer/test/Datadog.Trace.Tests/Util/SpanFactory.cs` (create file if missing):

```csharp
// <copyright file="SpanFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tests.Util;

internal static class SpanFactory
{
    internal static Span CreateSpan()
    {
        var traceContext = new TraceContext(new StubDatadogTracer());
        var spanContext = new SpanContext(parent: null, traceContext, serviceName: null);
        return new Span(spanContext, DateTimeOffset.UtcNow);
    }
}
```

Check whether `tracer/test/Datadog.Trace.Tests/Util/SpanFactory.cs` already exists with a `CreateSpan` method before creating it. The existing `SetHttpStatusCodeOtelTests.cs` has a private `CreateSpan` defined locally — a shared version does not yet exist at that path, so create the file above. If a shared version is added later by another task, remove the duplicate.

- [ ] **Step 2: Run tests to confirm they fail**

```
cd /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3/tracer
dotnet test test/Datadog.Trace.Tests/Datadog.Trace.Tests.csproj -f net8.0 --filter "FullyQualifiedName~DbOtelHelperTests" --no-build 2>&1 | tail -20
```

Expected: build error — `DbOtelHelper` does not exist.

- [ ] **Step 3: Create `DbOtelHelper.cs`**

Create `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AdoNet/DbOtelHelper.cs`:

```csharp
// <copyright file="DbOtelHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    internal static class DbOtelHelper
    {
        private static readonly Dictionary<string, string> DbSystemNameMap = new Dictionary<string, string>
        {
            [DbType.PostgreSql] = "postgresql",
            [DbType.SqlServer] = "microsoft.sql_server",
            [DbType.MySql] = "mysql",
            [DbType.Oracle] = "oracle.db",
            [DbType.Sqlite] = "sqlite",
        };

        internal static void SetDatabaseAttributes(
            ISpan span,
            string dbType,
            string? dbName,
            string? outHost,
            string? port,
            string commandText,
            bool peerServiceEnabled)
        {
            // db.system.name (always set)
            var systemName = DbSystemNameMap.TryGetValue(dbType, out var mapped) ? mapped : dbType;
            span.SetTag("db.system.name", systemName);

            // db.namespace
            if (!StringUtil.IsNullOrEmpty(dbName))
            {
                span.SetTag("db.namespace", dbName);
            }

            // server.address
            if (!StringUtil.IsNullOrEmpty(outHost))
            {
                span.SetTag("server.address", outHost);
            }

            // server.port
            if (!StringUtil.IsNullOrEmpty(port))
            {
                span.SetTag("server.port", port);
            }

            // db.query.text + db.operation.name + db.collection.name
            if (!StringUtil.IsNullOrEmpty(commandText))
            {
                span.SetTag("db.query.text", commandText);

                var (operation, table) = SqlQueryParser.Parse(commandText);
                if (!StringUtil.IsNullOrEmpty(operation))
                {
                    span.SetTag("db.operation.name", operation);
                }

                if (!StringUtil.IsNullOrEmpty(table))
                {
                    span.SetTag("db.collection.name", table);
                }
            }

            // peer.service (V1 schema compatibility)
            if (peerServiceEnabled)
            {
                var peerService = dbName ?? outHost;
                if (!StringUtil.IsNullOrEmpty(peerService))
                {
                    span.SetTag(Tags.PeerService, peerService);
                }
            }
        }
    }
}
```

- [ ] **Step 4: Build and run tests**

```
cd /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3/tracer
dotnet build src/Datadog.Trace/Datadog.Trace.csproj -f net8.0 -c Debug 2>&1 | tail -10
dotnet test test/Datadog.Trace.Tests/Datadog.Trace.Tests.csproj -f net8.0 --filter "FullyQualifiedName~DbOtelHelperTests" --no-build 2>&1 | tail -20
```

Expected: All tests pass.

- [ ] **Step 5: Commit**

```
git -C /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3 add \
  tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AdoNet/DbOtelHelper.cs \
  tracer/test/Datadog.Trace.Tests/ClrProfiler/AutoInstrumentation/AdoNet/DbOtelHelperTests.cs \
  tracer/test/Datadog.Trace.Tests/Util/SpanFactory.cs
git -C /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3 commit -m "Add DbOtelHelper for OTel DB semantic conventions"
```

---

### Task 4: Wire DbScopeFactory to Use OTel Path

**Files:**
- Modify: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AdoNet/DbScopeFactory.cs`

**Interfaces:**
- Consumes: `DbOtelHelper.SetDatabaseAttributes(ISpan, string, string?, string?, string?, string, bool)`, `TagsCacheItem.Port`
- Produces: modified `CreateDbCommandScope` that calls `DbOtelHelper` when `OpenTelemetrySemanticsEnabled` is true; modified `HasDbType` that handles OTel mode

- [ ] **Step 1: Update `CreateDbCommandScope`**

In `DbScopeFactory.CreateDbCommandScope` (the private overload, lines ~28–146), make these two changes:

**Change 1** — branch on OTel semantics when setting tags (after `scope = tracer.StartActiveInternal(...)`):

Replace the existing tag-setting block:
```csharp
tags = perTraceSettings.Schema.Database.CreateSqlTags();
tags.DbType = dbType;
tags.InstrumentationName = IntegrationRegistry.GetName(integrationId);
tags.DbName = tagsFromConnectionString.DbName;
tags.DbUser = tagsFromConnectionString.DbUser;
tags.OutHost = tagsFromConnectionString.OutHost;

tags.SetAnalyticsSampleRate(integrationId, perTraceSettings.Settings, enabledWithGlobalSetting: false);
perTraceSettings.Schema.RemapPeerService(tags);

scope = tracer.StartActiveInternal(operationName, tags: tags, serviceName: serviceName, serviceNameSource: serviceNameSource);
scope.Span.ResourceName = commandText;
scope.Span.Type = SpanTypes.Sql;
```

With:
```csharp
tags = perTraceSettings.Schema.Database.CreateSqlTags();
tags.InstrumentationName = IntegrationRegistry.GetName(integrationId);
tags.SetAnalyticsSampleRate(integrationId, perTraceSettings.Settings, enabledWithGlobalSetting: false);

scope = tracer.StartActiveInternal(operationName, tags: tags, serviceName: serviceName, serviceNameSource: serviceNameSource);
scope.Span.ResourceName = commandText;
scope.Span.Type = SpanTypes.Sql;

if (perTraceSettings.Settings.OpenTelemetrySemanticsEnabled)
{
    DbOtelHelper.SetDatabaseAttributes(
        span: scope.Span,
        dbType: dbType,
        dbName: tagsFromConnectionString.DbName,
        outHost: tagsFromConnectionString.OutHost,
        port: tagsFromConnectionString.Port,
        commandText: commandText,
        peerServiceEnabled: perTraceSettings.Schema.PeerServiceTagsEnabled);
}
else
{
    tags.DbType = dbType;
    tags.DbName = tagsFromConnectionString.DbName;
    tags.DbUser = tagsFromConnectionString.DbUser;
    tags.OutHost = tagsFromConnectionString.OutHost;
    perTraceSettings.Schema.RemapPeerService(tags);
}
```

**Change 2** — update `HasDbType` local function to handle OTel mode (at the bottom of `CreateDbCommandScope`):

Replace:
```csharp
static bool HasDbType(Span span, string dbType)
{
    if (span.Tags is SqlTags sqlTags)
    {
        return sqlTags.DbType == dbType;
    }

    return span.GetTag(Tags.DbType) == dbType;
}
```

With:
```csharp
static bool HasDbType(Span span, string dbType)
{
    if (span.Tags is SqlTags sqlTags && sqlTags.DbType is not null)
    {
        return sqlTags.DbType == dbType;
    }

    // OTel mode: db.type not set; check db.system.name instead
    var systemName = span.GetTag("db.system.name");
    if (systemName is not null)
    {
        return DbOtelHelper.GetSystemName(dbType) == systemName;
    }

    return span.GetTag(Tags.DbType) == dbType;
}
```

Also add `internal static string GetSystemName(string dbType)` to `DbOtelHelper` (used by `HasDbType`):

```csharp
internal static string GetSystemName(string dbType)
{
    return DbSystemNameMap.TryGetValue(dbType, out var mapped) ? mapped : dbType;
}
```

- [ ] **Step 2: Determine `peerServiceEnabled` from tags instance**

`NamingSchema._peerServiceTagsEnabled` is private and not exposed. Instead, detect peer-service mode by checking whether `CreateSqlTags()` returned a `SqlV1Tags` instance — that is the authoritative signal that peer.service is active.

In the `if (perTraceSettings.Settings.OpenTelemetrySemanticsEnabled)` branch, use:

```csharp
DbOtelHelper.SetDatabaseAttributes(
    span: scope.Span,
    dbType: dbType,
    dbName: tagsFromConnectionString.DbName,
    outHost: tagsFromConnectionString.OutHost,
    port: tagsFromConnectionString.Port,
    commandText: commandText,
    peerServiceEnabled: tags is SqlV1Tags);
```

`tags is SqlV1Tags` is `true` when V1 schema is active (peer.service derivation enabled), and `false` for V0.

- [ ] **Step 3: Build**

```
cd /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3/tracer
dotnet build src/Datadog.Trace/Datadog.Trace.csproj -f net8.0 -c Debug 2>&1 | tail -15
```

Expected: Build succeeds.

- [ ] **Step 4: Run unit tests to confirm no regressions**

```
cd /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3/tracer
dotnet test test/Datadog.Trace.Tests/Datadog.Trace.Tests.csproj -f net8.0 --filter "FullyQualifiedName~AdoNet|FullyQualifiedName~DbOtelHelper|FullyQualifiedName~SqlQueryParser" --no-build 2>&1 | tail -20
```

Expected: All pass.

- [ ] **Step 5: Commit**

```
git -C /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3 add \
  tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AdoNet/DbScopeFactory.cs \
  tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AdoNet/DbOtelHelper.cs
git -C /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3 commit -m "Wire DbScopeFactory to emit OTel DB attributes when OTel semantics enabled"
```

---

### Task 5: Test Infrastructure + Integration Tests + Snapshots

**Files:**
- Modify: `tracer/test/Datadog.Trace.TestHelpers/SpanMetadataOTelRules.cs` — update `db.system` → `db.system.name` in all 5 SQL OTel rules
- Modify: `tracer/test/Datadog.Trace.TestHelpers/SpanMetadataAPI.cs` — add `"otel"` dispatch case for `IsNpgsql`, `IsMySql`, `IsSqlClient`, `IsOracle`, `IsSqlite`
- Modify: `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/AdoNet/NpgsqlCommandTests.cs` — add `NpgsqlCommandOtelTests` class
- Modify: `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/AdoNet/SystemDataSqlClientTests.cs` — add `SystemDataSqlClientOtelTests` class
- Modify: `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/AdoNet/MySqlCommandTests.cs` — add `MySqlCommandOtelTests` class
- Create: snapshot files (auto-generated by running integration tests in `--accept` mode)

**Interfaces:**
- Consumes: `DbOtelHelper` via running instrumented test apps

- [ ] **Step 1: Update `SpanMetadataOTelRules.cs` — rename `db.system` → `db.system.name`**

In `tracer/test/Datadog.Trace.TestHelpers/SpanMetadataOTelRules.cs`, update all five SQL OTel rules. Change `.Matches("db.system", ...)` to `.Matches("db.system.name", ...)` and fix the value for SqlClient:

| Method | Old | New |
|---|---|---|
| `IsSqlClientOTel` | `.Matches("db.system", "mssql")` | `.Matches("db.system.name", "microsoft.sql_server")` |
| `IsMySqlOTel` | `.Matches("db.system", "mysql")` | `.Matches("db.system.name", "mysql")` |
| `IsNpgsqlOTel` | `.Matches("db.system", "postgresql")` | `.Matches("db.system.name", "postgresql")` |
| `IsSqliteOTel` | `.Matches("db.system", "sqlite")` | `.Matches("db.system.name", "sqlite")` |
| `IsOracleOTel` | `.Matches("db.system", "oracle")` | `.Matches("db.system.name", "oracle.db")` |

Also add `.IsPresent("db.query.text")` to `IsSqlClientOTel`, `IsMySqlOTel`, `IsNpgsqlOTel`, `IsOracleOTel` (it's a string tag now always set when commandText is non-empty). Keep it `.IsOptional` for `IsSqliteOTel` since SQLite is sometimes used with empty commands.

- [ ] **Step 2: Update `SpanMetadataAPI.cs` — add `"otel"` dispatch cases**

In `tracer/test/Datadog.Trace.TestHelpers/SpanMetadataAPI.cs`, update the five SQL dispatcher methods to add an `"otel"` case:

```csharp
public static Result IsNpgsql(this MockSpan span, string metadataSchemaVersion) =>
    metadataSchemaVersion switch
    {
        "v1" => span.IsNpgsqlV1(),
        "otel" => span.IsNpgsqlOTel(),     // new
        _ => span.IsNpgsqlV0(),
    };

public static Result IsMySql(this MockSpan span, string metadataSchemaVersion) =>
    metadataSchemaVersion switch
    {
        "v1" => span.IsMySqlV1(),
        "otel" => span.IsMySqlOTel(),      // new
        _ => span.IsMySqlV0(),
    };

public static Result IsSqlClient(this MockSpan span, string metadataSchemaVersion) =>
    metadataSchemaVersion switch
    {
        "v1" => span.IsSqlClientV1(),
        "otel" => span.IsSqlClientOTel(),  // new
        _ => span.IsSqlClientV0(),
    };

public static Result IsOracle(this MockSpan span, string metadataSchemaVersion) =>
    metadataSchemaVersion switch
    {
        "v1" => span.IsOracleV1(),
        "otel" => span.IsOracleOTel(),     // new
        _ => span.IsOracleV0(),
    };

public static Result IsSqlite(this MockSpan span, string metadataSchemaVersion) =>
    metadataSchemaVersion switch
    {
        "v1" => span.IsSqliteV1(),
        "otel" => span.IsSqliteOTel(),     // new
        _ => span.IsSqliteV0(),
    };
```

- [ ] **Step 3: Add `NpgsqlCommandOtelTests` class to `NpgsqlCommandTests.cs`**

Append to the end of `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/AdoNet/NpgsqlCommandTests.cs` (inside the namespace, after the closing `}` of `NpgsqlCommandTests`):

```csharp
[Trait("RequiresDockerDependency", "true")]
[Trait("DockerGroup", "1")]
[UsesVerify]
public class NpgsqlCommandOtelTests : TracingIntegrationTest
{
    public NpgsqlCommandOtelTests(ITestOutputHelper output)
        : base("Npgsql", output)
    {
        SetServiceVersion("1.0.0");
        SetEnvironmentVariable("DD_TRACE_OTEL_SEMANTICS_ENABLED", "true");
    }

    public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
        span.IsNpgsql("otel");

    [SkippableTheory]
    [CombinatorialOrPairwiseData]
    [Trait("Category", "EndToEnd")]
    public async Task SubmitsTracesOtel(
        [PackageVersionData(nameof(PackageVersions.Npgsql))] string packageVersion,
        [DbmPropagationModesData] string dbmPropagation)
    {
        SetEnvironmentVariable("DD_DBM_PROPAGATION_MODE", dbmPropagation);

        const int expectedSpanCount = 147;
        const string dbType = "postgres";
        const string expectedOperationName = dbType + ".query";

        var clientSpanServiceName = $"{EnvironmentHelper.FullSampleName}-{dbType}";

        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        using var process = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
        var spans = await agent.WaitForSpansAsync(expectedSpanCount, operationName: expectedOperationName);
        int actualSpanCount = spans.Count(s => s.ParentId.HasValue);
        var filteredSpans = spans.Where(s => s.ParentId.HasValue).ToList();

        actualSpanCount.Should().Be(expectedSpanCount);
        ValidateIntegrationSpans(spans, metadataSchemaVersion: "otel", expectedServiceName: clientSpanServiceName, isExternalSpan: true);
        await telemetry.AssertIntegrationEnabledAsync(IntegrationId.Npgsql);

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddRegexScrubber(new Regex("Npgsql-Test-[a-zA-Z0-9]{32}"), "Npgsql-Test-GUID");
        settings.AddSimpleScrubber("server.address: localhost", "server.address: postgres");
        settings.AddSimpleScrubber("server.address: postgres_arm64", "server.address: postgres");

        var fileName = nameof(NpgsqlCommandOtelTests);
#if NETFRAMEWORK
        fileName = fileName + ".Net462";
#endif
        fileName = fileName + (dbmPropagation switch
        {
            "full" => ".tagged",
            _ => ".untagged",
        });

        await VerifyHelper.VerifySpans(filteredSpans, settings)
                          .DisableRequireUniquePrefix()
                          .UseFileName($"{fileName}.OtelSemantics");
    }
}
```

- [ ] **Step 4: Add `SystemDataSqlClientOtelTests` class**

In `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/AdoNet/SystemDataSqlClientTests.cs`, examine the existing `SubmitsTraces` method structure (look for the `expectedSpanCount`, `dbType`, `expectedOperationName` values and `ValidateIntegrationSpan`). Then append a new class following the same pattern as above but for SQL Server:

```csharp
[Trait("RequiresDockerDependency", "true")]
[Trait("DockerGroup", "1")]
[UsesVerify]
public class SystemDataSqlClientOtelTests : TracingIntegrationTest
{
    public SystemDataSqlClientOtelTests(ITestOutputHelper output)
        : base("SqlServer", output)
    {
        SetServiceVersion("1.0.0");
        SetEnvironmentVariable("DD_TRACE_OTEL_SEMANTICS_ENABLED", "true");
    }

    public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
        span.IsSqlClient("otel");

    [SkippableTheory]
    [CombinatorialOrPairwiseData]
    [Trait("Category", "EndToEnd")]
    public async Task SubmitsTracesOtel(
        [PackageVersionData(nameof(PackageVersions.SystemDataSqlClient))] string packageVersion,
        [DbmPropagationModesData] string dbmPropagation)
    {
        SetEnvironmentVariable("DD_DBM_PROPAGATION_MODE", dbmPropagation);

        // Use same expectedSpanCount as the non-OTel SystemDataSqlClientTests.SubmitsTraces
        const string dbType = "sql-server";
        const string expectedOperationName = dbType + ".query";

        var clientSpanServiceName = $"{EnvironmentHelper.FullSampleName}-{dbType}";

        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        using var process = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
        var spans = await agent.WaitForSpansAsync(operationName: expectedOperationName);
        var filteredSpans = spans.Where(s => s.ParentId.HasValue).ToList();

        ValidateIntegrationSpans(spans, metadataSchemaVersion: "otel", expectedServiceName: clientSpanServiceName, isExternalSpan: true);
        await telemetry.AssertIntegrationEnabledAsync(IntegrationId.SqlClient);

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddSimpleScrubber("server.address: localhost", "server.address: sqlserver");

        var fileName = nameof(SystemDataSqlClientOtelTests);
#if NETFRAMEWORK
        fileName = fileName + ".Net462";
#endif
        fileName = fileName + (dbmPropagation switch
        {
            "full" => ".tagged",
            _ => ".untagged",
        });

        await VerifyHelper.VerifySpans(filteredSpans, settings)
                          .DisableRequireUniquePrefix()
                          .UseFileName($"{fileName}.OtelSemantics");
    }
}
```

**Note:** Check `SystemDataSqlClientTests.cs` for the actual `expectedSpanCount` and `PackageVersions` property name before adding this class — adjust accordingly.

- [ ] **Step 5: Add `MySqlCommandOtelTests` class**

In `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/AdoNet/MySqlCommandTests.cs`, append a new class following the same pattern. Use `dbType = "mysql"`, `IntegrationId.MySql`, `ValidateIntegrationSpan` routing to `span.IsMySql("otel")`.

```csharp
[Trait("RequiresDockerDependency", "true")]
[Trait("DockerGroup", "1")]
[UsesVerify]
public class MySqlCommandOtelTests : TracingIntegrationTest
{
    public MySqlCommandOtelTests(ITestOutputHelper output)
        : base("MySql", output)
    {
        SetServiceVersion("1.0.0");
        SetEnvironmentVariable("DD_TRACE_OTEL_SEMANTICS_ENABLED", "true");
    }

    public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
        span.IsMySql("otel");

    [SkippableTheory]
    [CombinatorialOrPairwiseData]
    [Trait("Category", "EndToEnd")]
    public async Task SubmitsTracesOtel(
        [PackageVersionData(nameof(PackageVersions.MySql))] string packageVersion,
        [DbmPropagationModesData] string dbmPropagation)
    {
        SetEnvironmentVariable("DD_DBM_PROPAGATION_MODE", dbmPropagation);

        const string dbType = "mysql";
        const string expectedOperationName = dbType + ".query";

        var clientSpanServiceName = $"{EnvironmentHelper.FullSampleName}-{dbType}";

        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        using var process = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
        var spans = await agent.WaitForSpansAsync(operationName: expectedOperationName);
        var filteredSpans = spans.Where(s => s.ParentId.HasValue).ToList();

        ValidateIntegrationSpans(spans, metadataSchemaVersion: "otel", expectedServiceName: clientSpanServiceName, isExternalSpan: true);
        await telemetry.AssertIntegrationEnabledAsync(IntegrationId.MySql);

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddSimpleScrubber("server.address: localhost", "server.address: mysql");

        var fileName = nameof(MySqlCommandOtelTests);
#if NETFRAMEWORK
        fileName = fileName + ".Net462";
#endif
        fileName = fileName + (dbmPropagation switch
        {
            "full" => ".tagged",
            _ => ".untagged",
        });

        await VerifyHelper.VerifySpans(filteredSpans, settings)
                          .DisableRequireUniquePrefix()
                          .UseFileName($"{fileName}.OtelSemantics");
    }
}
```

- [ ] **Step 6: Build the integration test project**

```
cd /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3/tracer
dotnet build test/Datadog.Trace.ClrProfiler.IntegrationTests/Datadog.Trace.ClrProfiler.IntegrationTests.csproj -f net8.0 -c Debug 2>&1 | tail -15
```

Fix any compilation errors (e.g., missing `using` directives, wrong `PackageVersions` property names). Check the existing `MySqlCommandTests.cs` for the exact `PackageVersions.MySql` property name — it may be `PackageVersions.MySqlData` or similar.

- [ ] **Step 7: Generate snapshot files**

Start Docker dependencies, then run the new OTel integration tests with Verify's `--accept` flag to create the initial snapshots. Run one integration test class at a time:

```bash
# Ensure Docker is running with DB containers
docker compose -f /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3/docker-compose.yml up -d postgres mysql sqlserver

# Run Npgsql OTel test (accept mode creates snapshots)
cd /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3/tracer
dotnet test test/Datadog.Trace.ClrProfiler.IntegrationTests/Datadog.Trace.ClrProfiler.IntegrationTests.csproj \
  -f net8.0 \
  --filter "FullyQualifiedName~NpgsqlCommandOtelTests" \
  -e "Verify.UseDirectory=../test/snapshots" \
  -- RunSettings.DiffEngine=none 2>&1 | tail -30
```

If Verify creates `.received.txt` files instead of `.verified.txt`, approve them:
```bash
find /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3/tracer/test/snapshots -name "*.received.txt" | \
  while read f; do mv "$f" "${f/.received.txt/.verified.txt}"; done
```

Repeat for `SystemDataSqlClientOtelTests` and `MySqlCommandOtelTests`.

**Verify snapshot contents** — open one of the new `.OtelSemantics.verified.txt` snapshots and confirm:
- `db.system.name` is present (e.g. `db.system.name: postgresql`)
- `db.namespace` is present
- `server.address` is present
- `db.query.text` is present
- `db.type` is **absent**
- `db.name` is **absent**
- `out.host` is **absent**

- [ ] **Step 8: Run full unit test suite to confirm no regressions**

```
cd /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3/tracer
dotnet test test/Datadog.Trace.Tests/Datadog.Trace.Tests.csproj -f net8.0 --no-build 2>&1 | tail -20
```

Expected: All existing tests pass.

- [ ] **Step 9: Commit**

```
git -C /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3 add \
  tracer/test/Datadog.Trace.TestHelpers/SpanMetadataOTelRules.cs \
  tracer/test/Datadog.Trace.TestHelpers/SpanMetadataAPI.cs \
  tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/AdoNet/NpgsqlCommandTests.cs \
  tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/AdoNet/SystemDataSqlClientTests.cs \
  tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/AdoNet/MySqlCommandTests.cs \
  tracer/test/snapshots/
git -C /Users/zach.montoya/.supacode/repos/dd-trace-dotnet/ai-week-3 commit -m "Add OTel DB semantics integration tests and update span metadata rules"
```
