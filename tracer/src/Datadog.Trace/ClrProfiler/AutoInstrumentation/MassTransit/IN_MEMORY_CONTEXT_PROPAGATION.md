# In-Memory (Loopback) Context Propagation for MassTransit 7

## Problem

MassTransit 7 supports an in-memory (loopback) transport for local message delivery within the same
process. When using this transport, trace context injected into `SendContext.Headers` at send time
is **not** propagated to the receive side in versions prior to 7.3.0.

### Why It Breaks

In `InMemorySendTransport.Send<T>()`, the flow is:

```
1. pipe.Send(context)          ← DiagnosticObserver fires here; we inject trace headers into context.Headers
2. InMemoryTransportMessage created from context.Body, MessageId, ContentType (NOT from context.Headers)
3. Exchange.Send(message)      ← message dispatched; receive side fires shortly after
```

- In MT **7.0.0–7.2.x**: `InMemoryTransportMessage` is created without copying `context.Headers`.
  The injected Datadog headers are lost at step 2.
- In MT **7.3.0+**: `SetHeaders(inMemoryTransportMessage.Headers, context.Headers)` is called between
  steps 2 and 3, natively copying all headers. Context propagation works without any workaround.

---

## Selected Approach: ConcurrentDictionary keyed by MessageId

**Implementation**: `MassTransitDiagnosticObserver.InMemoryContextByMessageId`

At `Send.Start`, after creating the produce span, store the span's `SpanContext` keyed by
`MessageId` (Guid). At `Receive.Start`, if header-based extraction fails (loopback, MT <7.3),
read `MessageId` from `TransportHeaders` and retrieve the stored `SpanContext`.

```
OnProduceStart → create span → store SpanContext by messageId.Value
OnReceiveStart → try TransportHeaders extract (works for RabbitMQ/SQS and MT7.3+ loopback)
              → try ReceiveContext.Headers extract (fallback)
              → try ConcurrentDictionary lookup by MessageId (MT7 <7.3 loopback)
```

**Why MessageId is a reliable key:**
- Send side: `messageId` comes from `MessageSendContext.MessageId` (set by MassTransit before firing
  the diagnostic event, always non-null in practice)
- Receive side: `InMemoryTransportMessage` constructor always sets `Headers["MessageId"] = messageId.ToString()`
- Both sides reference the same Guid — guaranteed by MassTransit's own code:
  `Guid messageId = context.MessageId ?? NewId.NextGuid()` — if `MessageId` is null on both sides
  simultaneously, they'd each generate a different Guid; this is a benign failure (no parent, not a crash)

**Memory safety:** `TryRemove` is called on retrieval, so entries don't accumulate.

---

## Approaches Considered and Rejected

### 1. CallTarget on `InMemorySendTransport.Send<T>()`

**Idea:** Hook the send method to copy `context.Headers` to `InMemoryTransportMessage.Headers`
before `Exchange.Send()`.

**Why it doesn't work:**
```csharp
async Task ISendTransport.Send<T>(T message, IPipe<SendContext<T>> pipe, CancellationToken cancellationToken)
{
    MessageSendContext<T> context = new MessageSendContext<T>(message, cancellationToken); // local var
    await pipe.Send((SendContext<T>)context);    // ← DiagnosticObserver fires here
    InMemoryTransportMessage message2 = new InMemoryTransportMessage(...);                 // local var
    await _context.Exchange.Send(message2, cancellationToken);
}
```

`context` and `message2` are **both local variables** inside the method body. A CallTarget hook on
`ISendTransport.Send()` receives only the method parameters (`T message`, `IPipe<...> pipe`,
`CancellationToken`) — it cannot access either local variable. There is no hook point that provides
simultaneous access to both `context.Headers` (populated after `pipe.Send()`) and `message2`.

### 2. ISendObserver.PostSend()

**Idea:** Hook `ISendObserver.PostSend()` which fires after a successful send.

**Why it doesn't work:**
- `ISendObserver.PostSend()` is called after `Exchange.Send()` completes — the message is already
  in the queue. Even if we could write to `InMemoryTransportMessage.Headers` at this point, the
  receive context has already been created from the message object and the headers are stale.
- Also, `PostSend` passes only `SendContext<T>`, not `InMemoryTransportMessage`, so we have no way
  to write to the transport message's headers.

### 3. CallTarget on `InMemoryTransportMessage` constructor

**Idea:** Hook the constructor to capture the `InMemoryTransportMessage` instance, then write
headers to it after `pipe.Send()` completes.

**Why it doesn't work:**
- The constructor receives `messageId, body, contentType, messageType` — no headers.
- We'd need to correlate the captured instance back to the correct send operation. While possible
  using a separate `ConditionalWeakTable<InMemoryTransportMessage, ...>`, we still have no way to
  write to it AFTER `pipe.Send()` (which populates the trace headers) but BEFORE `Exchange.Send()`.
- The window between those two calls is synchronous and not hookable from outside.

### 4. AsyncLocal<SpanContext>

**Idea:** Store the current `SpanContext` in an `AsyncLocal<SpanContext>` at `Send.Start`,
read it at `Receive.Start`. No MessageId needed.

**Why it's risky:**
- In-memory delivery is synchronous — `Receive.Start` fires on the same async continuation as the
  send. So `AsyncLocal.Value` would still hold the send span's context at receive time.
- **However**: if multiple messages are sent concurrently on the same async flow (e.g. `Task.WhenAll`
  on multiple sends), the last write to `AsyncLocal` wins, and earlier sends' contexts are lost.
- MassTransit commonly sends multiple messages in parallel (e.g. saga state machines sending
  multiple events). This makes `AsyncLocal` unsafe.

### 5. ConditionalWeakTable

**Idea:** Use `ConditionalWeakTable<TKey, SpanContext>` for automatic GC-based cleanup instead of
`ConcurrentDictionary` with manual `TryRemove`.

**Why it doesn't apply:**
- `ConditionalWeakTable<TKey, TValue>` requires `TKey : class` (reference type constraint).
- `Guid` is a value type (struct) and cannot be used as a key.
- Boxing the Guid would create a reference type, but the boxed instance would have no other strong
  references keeping it alive — it would be GC'd before `Receive.Start` fires.
- The only viable key would be `InMemoryTransportMessage` itself (reference type), but we don't
  have access to it at send time from the DiagnosticObserver.

---

## Version Matrix

| MT7 Version | Loopback propagation | Mechanism |
|-------------|---------------------|-----------|
| 7.0.0–7.2.x | ❌ native, ✅ via dict | `ConcurrentDictionary` fallback |
| 7.3.0+      | ✅ native             | `SetHeaders()` copies headers; dict stored but never consumed |

For MT7.3.0+, the `ConcurrentDictionary` entry is stored but `TryRemove` returns true at receive time
(MT7.3+ transport headers contain Datadog headers, so attempt #1 succeeds before the fallback fires).
Wait — actually for MT7.3+ loopback, attempt #1 (TransportHeaders extraction) now succeeds natively,
so the dictionary lookup is skipped AND the stored entry leaks. To prevent this, the loopback skip
ensures that for MT7.3+ loopback, attempt #1 is skipped and the dictionary path always fires.

See `MassTransitDiagnosticObserver.OnReceiveStart` — the `isLoopback` check forces the
`ConcurrentDictionary` path for all loopback addresses regardless of MT7 version, ensuring
consistent behavior.
