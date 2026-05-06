# HTTP Client Instrumentation

HTTP client integrations provide observability into outgoing HTTP requests.

## What to Trace

### Request Execution (Critical)
Every HTTP request sent over the wire:
- Request methods: `request()`, `get()`, `post()`, `put()`, `delete()`, `patch()`
- Fetch API: `fetch()`
- Each individual network attempt matters

### Retries and Redirects
- Each retry attempt (with retry count)
- Following redirects (3xx responses)
- Authorization retries (401 responses)

**Key principle**: Trace every network attempt, not just the final result.

## What to Skip

### Client Setup
- Client instantiation: `new HttpClient()`, `axios.create()`
- Configuration: setting headers, timeouts, interceptors
- Agent creation: `new http.Agent()`

### Request Building
- Request builders without execution
- URL construction
- Header preparation

### Internal Mechanics
- Connection pooling
- Keep-alive management
- Request serialization

## Context Propagation

HTTP clients **inject** trace context into outgoing request headers to propagate traces to downstream services.
