# Database Instrumentation

Database integrations provide observability into query execution for SQL and NoSQL databases.

## What to Trace

### Query Execution (Critical)
All database client calls that execute queries or commands:
- Query methods: `query()`, `execute()`, `command()`
- Find operations: `find()`, `findOne()`, `findMany()`
- Write operations: `insert()`, `update()`, `delete()`, `remove()`
- Aggregate operations: `aggregate()`, `count()`

### Transaction Boundaries
- Transaction control: `BEGIN`, `COMMIT`, `ROLLBACK`
- These are traced as regular query operations

### Batch/Bulk Operations
- Batch writes: `bulkWrite()`, `batchExecute()`
- Multi-statement execution

## What to Skip

### Connection Management
- Connection creation: `createConnection()`, `connect()`
- Connection pooling: `pool.getConnection()`, `pool.acquire()`
- Connection validation: health checks, ping

### Query Building
- Query builders that don't execute: `select().from().where()` without `.execute()`
- Prepared statement creation (trace execution, not preparation)

### Result Processing
- Result parsing, ORM hydration
- Cursor iteration (trace cursor creation, not each iteration)

### Infrastructure
- Authentication handshakes
- Protocol-level operations
- Internal heartbeats (MongoDB `hello`, `ismaster`)

## Context Propagation

Database operations typically do NOT require context propagation - they are leaf spans that inherit context from the current trace.

**Exception**: Database Monitoring (DBM) may inject trace context into query comments for database-side correlation.

## ORM Libraries

ORMs (Sequelize, TypeORM, Prisma, Mongoose) follow database patterns - instrument the underlying driver operations, not ORM-level abstractions.
