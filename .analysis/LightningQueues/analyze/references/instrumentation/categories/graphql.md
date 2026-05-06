# GraphQL Instrumentation

GraphQL integrations provide observability into GraphQL query execution for servers and clients.

## What to Trace

### Execution Phases (Critical)

**Parse**
- Query string parsing into AST
- Captures syntax errors early

**Validate**
- Schema validation of parsed document
- Captures validation errors

**Execute**
- Root query/mutation/subscription execution
- The primary operation span

### Operation Types
All three GraphQL operation types:
- **Queries**: Read operations for data fetching
- **Mutations**: Write operations that modify data
- **Subscriptions**: Long-lived operations for real-time data

### Resolver Execution (Optional)
- Individual field resolver execution
- Can be noisy for complex queries - often configurable
- Useful for identifying slow resolvers

## What to Skip

### Schema Building
- Schema definition and construction
- Type registration
- Directive setup

### Client-Side Query Building
- Query string construction
- Variable preparation

### Internal GraphQL Operations
- AST manipulation
- Type coercion
- Result formatting

## Context Propagation

**GraphQL Servers**: Extract context from incoming HTTP request headers (handled by HTTP server instrumentation).

**GraphQL Clients**: Inject context into outgoing HTTP request headers (handled by HTTP client instrumentation).

GraphQL-specific propagation is typically not needed - rely on underlying HTTP transport.

## Apollo Gateway

For federated GraphQL (Apollo Gateway), additional operations matter:
- Query planning
- Subgraph fetches
- Response composition
