# RPC Instrumentation

RPC integrations provide observability into remote procedure calls for gRPC, Thrift, and similar protocols.

## What to Trace

### Client Operations (Critical)
All RPC method invocations from client to server:
- Unary calls: single request, single response
- Server streaming: single request, multiple responses
- Client streaming: multiple requests, single response
- Bidirectional streaming: multiple requests and responses

The span covers from RPC initiation to response receipt (or error).

### Server Operations (Critical)
All incoming RPC method handlers:
- Unary handlers
- Streaming handlers

The span covers from request receipt to response transmission.

### Streaming Lifecycle
- Full lifetime of request/response streams until closure
- Stream errors and cancellations

## What to Skip

### Channel/Connection Setup
- Channel creation
- Connection establishment
- Credential configuration

### Code Generation
- Stub generation
- Service definition loading

### Internal Protocol Operations
- Serialization/deserialization
- Compression
- Flow control

## Context Propagation

**RPC Clients**: Inject trace context into RPC metadata/headers before sending.

**RPC Servers**: Extract trace context from incoming RPC metadata/headers.

This enables distributed tracing across RPC service boundaries.

## Streaming Considerations

For streaming RPCs:
- One span covers the entire stream lifecycle
- Stream errors should be captured
- Cancellation (client or server initiated) should be recorded
