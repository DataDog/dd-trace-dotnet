# HTTP Server Instrumentation

HTTP server integrations provide observability into incoming request handling for web frameworks and HTTP servers.

## What to Trace

### Request Handling (Critical)
The **internal request handler** that processes each incoming request:
- The method called per-request to handle the request/response lifecycle
- Framework internals: `handleRequest()`, `handle()`, `dispatch()`

**Key distinction**: Trace internal handlers, NOT public route registration APIs.

```
WRONG:  app.get('/users', handler)    // Route registration (setup time)
WRONG:  app.use(middleware)           // Middleware registration (setup time)
WRONG:  app.listen(3000)              // Server startup

RIGHT:  Layer.prototype.handle_request  // Express internal per-request
RIGHT:  Application.prototype.handleRequest  // Koa internal per-request
RIGHT:  Server internal request dispatch    // Where each request is processed
```

### Middleware Execution
- The internal function that invokes each middleware in the chain
- Per-request middleware processing (optional, can be noisy)

## What to Skip

### Application Setup
- Route registration: `app.get()`, `app.post()`, `router.use()`
- Middleware registration: `app.use(middleware)`
- Server startup: `app.listen()`, `server.start()`
- Configuration: `app.set()`, `app.configure()`

### Factory Methods
- Application creation: `express()`, `new Koa()`, `fastify()`
- Router creation: `express.Router()`

## Context Propagation

HTTP servers **extract** trace context from incoming request headers to continue distributed traces. The server span becomes a child of the upstream caller's span.

## Finding Internal Handlers

Pattern for identifying correct instrumentation points:
1. Find where `server.on('request')` or equivalent is handled
2. Trace the code path from request receipt to response send
3. Look for internal methods called **per-request** (not during setup)
4. Names often include: `handle`, `dispatch`, `process`, `execute`
