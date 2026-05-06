# Cache Instrumentation

Cache integrations provide observability into cache operations for Redis, Memcached, and similar systems.

## What to Trace

### Read Operations (Critical)
- Get operations: `get()`, `mget()`, `hget()`, `hgetall()`
- Existence checks: `exists()`, `sismember()`

### Write Operations (Critical)
- Set operations: `set()`, `mset()`, `hset()`, `setex()`
- Delete operations: `del()`, `hdel()`

### Collection Operations
- List operations: `lpush()`, `rpush()`, `lpop()`, `lrange()`
- Set operations: `sadd()`, `srem()`, `smembers()`
- Sorted set operations: `zadd()`, `zrange()`
- Hash operations: `hmset()`, `hmget()`

### Utility Operations
- TTL operations: `expire()`, `ttl()`, `persist()`
- Key operations: `keys()`, `scan()`

## What to Skip

### Connection Management
- Connection creation: `createClient()`, `connect()`
- Connection pooling
- Reconnection logic

### Pub/Sub (Separate Category)
- `subscribe()`, `publish()` - These follow messaging patterns, not cache patterns
- If the library uses Redis for pub/sub, that may warrant messaging instrumentation
- See {{link:instrumentation/categories/messaging|Messaging Instrumentation}}.

### Administration
- Server info: `info()`, `config()`
- Cluster management

## Context Propagation

Cache operations typically do NOT require context propagation - they are leaf spans that inherit context from the current trace.
