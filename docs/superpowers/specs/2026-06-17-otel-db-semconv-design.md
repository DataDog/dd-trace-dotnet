# OTel Database Semantic Conventions — Design Spec

**Date:** 2026-06-17  
**Branch:** `ai-week-3-db-semconv`  
**References:**
- Spec: https://opentelemetry.io/docs/specs/semconv/db/database-spans/
- System-tests: https://github.com/DataDog/system-tests/pull/7166
- Reference impl (dd-trace-js): https://github.com/DataDog/dd-trace-js/pull/8961

---

## Goal

When `DD_TRACE_OTEL_SEMANTICS_ENABLED=true`, SQL database spans (ADO.NET integrations: SqlServer, PostgreSQL, MySQL, SQLite, Oracle) emit OpenTelemetry database semantic convention attribute names instead of Datadog legacy names. Legacy names are absent when OTel mode is active. This satisfies the system-tests `OTEL_SEMANTICS_DB` scenario.

Non-SQL databases (MongoDB, Redis, Elasticsearch, CosmosDB) are out of scope for this PR.

---

## Architecture Overview

### New files

**`tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AdoNet/DbOtelHelper.cs`**  
Static helper mirroring `HttpOtelHelper`. Sets all 7 OTel attributes directly on an `ISpan` via `span.SetTag()` / `span.SetMetric()`. Owns the `db.type` → `db.system.name` value mapping and calls `SqlQueryParser` for the derived attributes.

**`tracer/src/Datadog.Trace/Util/SqlQueryParser.cs`**  
Zero-allocation SQL parser. Given a `CommandText` string, returns `(string? operation, string? table)`. Conservative: returns null for `table` when the query is ambiguous. Strips leading DBM `/* ... */` comment prefixes before parsing.

### Modified files

**`tracer/src/Datadog.Trace/Util/DbCommandCache.cs`**  
Extend `TagsCacheItem` with a `Port` field (nullable string). Extract from connection string keys: `Port`, `Port Number`.

**`tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AdoNet/DbScopeFactory.cs`**  
In `CreateDbCommandScope()`, after the scope is started, branch on `perTraceSettings.Settings.OpenTelemetrySemanticsEnabled`:
- **OTel mode on:** skip setting `tags.DbType`/`tags.DbName`/`tags.DbUser`/`tags.OutHost`; call `DbOtelHelper.SetDatabaseAttributes(scope.Span, ...)` instead.
- **OTel mode off:** existing behavior unchanged.

### Data flow

```
IDbCommand.Execute()
  → DbScopeFactory.CreateDbCommandScope()
      → [OTel off]  tags.DbType/DbName/DbUser/OutHost = ... (existing)
      → [OTel on]   DbOtelHelper.SetDatabaseAttributes(span, dbType, dbName, outHost, port, commandText)
                          → span.SetTag("db.system.name", ...)
                          → span.SetTag("db.namespace", ...)
                          → span.SetTag("server.address", ...)
                          → span.SetMetric("server.port", ...)
                          → span.SetTag("db.query.text", commandText)
                          → SqlQueryParser.Parse(commandText) → (operation, table)
                          → span.SetTag("db.operation.name", operation)   // if non-null
                          → span.SetTag("db.collection.name", table)      // if non-null
```

---

## Attribute Mapping

### `db.system.name` value mapping

| `DbType` constant | value | `db.system.name` |
|---|---|---|
| `DbType.PostgreSql` | `"postgres"` | `"postgresql"` |
| `DbType.SqlServer` | `"sql-server"` | `"microsoft.sql_server"` |
| `DbType.MySql` | `"mysql"` | `"mysql"` |
| `DbType.Oracle` | `"oracle"` | `"oracle.db"` |
| `DbType.Sqlite` | `"sqlite"` | `"sqlite"` |
| unknown | any other string | pass through as-is |

### Full attribute table

| OTel attribute | kind | source | condition |
|---|---|---|---|
| `db.system.name` | tag | `dbType` via map | Always set |
| `db.namespace` | tag | `TagsCacheItem.DbName` | Omit if null |
| `server.address` | tag | `TagsCacheItem.OutHost` | Omit if null |
| `server.port` | **metric** | `TagsCacheItem.Port` | Omit if null or non-integer; use `SetMetric()` for OTLP `intValue` serialization |
| `db.query.text` | tag | `command.CommandText` | Omit if empty |
| `db.operation.name` | tag | `SqlQueryParser` | Omit if parser returns null |
| `db.collection.name` | tag | `SqlQueryParser` | Omit when ambiguous |

### `peer.service` with V1 schema + OTel mode

`SqlV1Tags.PeerService` is auto-derived from `DbName ?? OutHost`. When OTel mode is on, `tags.DbName` and `tags.OutHost` are not set, so the auto-derivation yields null. `DbOtelHelper.SetDatabaseAttributes()` explicitly sets `peer.service` via `span.SetTag(Tags.PeerService, dbName ?? outHost)` when a value is available, preserving V1 behavior.

### Nested-span deduplication (`HasDbType`)

`DbScopeFactory` contains a local `HasDbType(Span span, string dbType)` helper that checks `sqlTags.DbType == dbType` to skip re-instrumenting the same SQL call from a nested stack frame. When OTel mode is on and `tags.DbType` is not set, this check always returns false, breaking deduplication.

Fix: `HasDbType` must also check `span.GetTag("db.system.name")` — compare against the mapped OTel value for the given `dbType` (e.g. `"postgres"` → check `db.system.name == "postgresql"`). The `DbOtelHelper` map is used for this lookup.

---

## SqlQueryParser

### Responsibilities

1. Strip leading `/* ... */` block comments (DBM injects `/*dddbs=...*/` prefixes).
2. Find the first SQL keyword → `db.operation.name`.
3. Extract the primary table identifier → `db.collection.name` (for unambiguous single-table cases only).

### Recognized operation names

`SELECT`, `INSERT`, `UPDATE`, `DELETE`, `CREATE`, `DROP`, `ALTER`, `MERGE`, `CALL`, `EXEC`, `EXECUTE`, `TRUNCATE`. Anything else → null (omit `db.operation.name`).

### Table extraction rules

| Verb | Rule |
|---|---|
| `SELECT` | First identifier after `FROM`, before `JOIN`/`,`/`WHERE`/`GROUP`/`ORDER`/`HAVING`/`LIMIT`. Null if `FROM` is absent, followed by `(` (subquery), or comma-separated. |
| `INSERT` | Identifier immediately after `INSERT INTO`. |
| `UPDATE` | First identifier after `UPDATE`, before `SET`. |
| `DELETE` | Identifier after `DELETE FROM`. |
| All others | Null. |

### Identifier normalization

- Strip surrounding quote characters: `"..."`, `` `...` ``, `[...]`
- Strip schema prefix: `schema.table` → `table`
- Result is the bare table name as written (case preserved)

### Cases that yield null collection

- Subquery in FROM: `SELECT * FROM (SELECT ...)`
- Multi-table FROM: `FROM a, b`
- CTEs: `WITH cte AS (...) SELECT ...`
- Stored procedures: `EXEC sp_name`
- Empty/null input

---

## Testing

### Unit tests — `SqlQueryParser`

New `SqlQueryParserTests.cs` in `Datadog.Trace.Tests`. Theory-driven table of `(input, expectedOperation, expectedTable)` covering:

- Simple SELECT / INSERT / UPDATE / DELETE (happy paths)
- Schema-qualified names: `SELECT * FROM dbo.Users` → table = `Users`
- All three quote styles: `"table"`, `` `table` ``, `[table]`
- DBM-prefixed query: `/*dddbs=mydb*/SELECT ...` → parses correctly
- Multi-table FROM: `FROM a, b` → null collection
- JOIN: `FROM a JOIN b` → null collection
- Subquery in FROM → null collection
- CTE (`WITH ... AS`) → null collection
- EXEC / stored proc → null collection
- Empty string → (null, null)

### Unit tests — `DbOtelHelper`

New `DbOtelHelperTests.cs` in `Datadog.Trace.Tests`. Covering:

- `db.system.name` value mapping for all 5 known DB types
- Unknown `db.type` passes through as-is
- `server.port` is set as a **metric** (not a tag)
- `peer.service` is set when `dbName` or `outHost` is non-null
- Null/empty fields produce no tag/metric (no-op)

### Integration test snapshots

New snapshot files following existing `SchemaV0`/`SchemaV1` naming convention, with an `OtelSemantics` variant (one per existing DB integration test class × TFM × tagged/untagged):

- `NpgsqlCommandTests.Net462.untagged.OtelSemantics.verified.txt`
- `NpgsqlCommandTests.Net462.tagged.OtelSemantics.verified.txt`
- `SystemDataSqlClientTests.tagged.OtelSemantics.verified.txt`
- `MySqlCommandTests.Net.tagged.OtelSemantics.verified.txt`

Each snapshot asserts:
- OTel names present (`db.system.name`, `db.namespace`, `server.address`, `db.query.text`)
- Legacy names absent (`db.type`, `db.name`, `out.host`)

### `SpanMetadataOTelRules.cs`

Add OTel DB span rules for `postgres.query`, `sqlserver.query`, `mysql.query`, `sqlite.query`, `oracle.query` asserting required OTel attribute presence and legacy attribute absence.

---

## Out of Scope

- Non-SQL databases: MongoDB, Redis, Elasticsearch, CosmosDB (separate OTel semconv specs)
- `db.query.text` sanitization (tracer-side literal → `?` replacement) — currently relies on agent-side obfuscation
- `db.response.status_code` on failed queries
- `db.query.summary` (low-cardinality span name rule)
- Export-time renaming layer
