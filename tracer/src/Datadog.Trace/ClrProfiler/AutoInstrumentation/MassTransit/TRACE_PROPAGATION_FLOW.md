# MassTransit Trace Context Propagation Flow

This document explains how distributed tracing headers are injected and extracted in the MassTransit instrumentation.

---

## Producer Side: INJECTING Headers (Outgoing Messages)

### Scenario
When a service **sends** a MassTransit message, we inject distributed tracing headers so the consumer can continue the trace.

### Call Flow

```
1. MassTransit Application Code
   └─> bus.Send(message)

2. Native Profiler (CallTarget) intercepts
   └─> SendIntegration.OnMethodEnd()

3. SendIntegration
   └─> MassTransitCommon.InjectTraceContext(tracer, sendContext, scope)

4. MassTransitCommon.InjectTraceContext
   ├─> sendContext.DuckCast<ISendContext>()          // Duck typing to get Headers property
   ├─> headers = sendContext.Headers
   ├─> new ProduceContextHeadersAdapter(headers)     // Create adapter for injection
   │   └─> Constructor uses reflection (GetInterfaceMap) to find Set methods
   │       and caches the MethodInfo for later use
   └─> SpanContextPropagator.Inject(context, adapter)

5. SpanContextPropagator.Inject
   ├─> adapter.Set("x-datadog-trace-id", traceId)
   ├─> adapter.Set("x-datadog-parent-id", spanId)
   ├─> adapter.Set("x-datadog-sampling-priority", priority)
   ├─> adapter.Set("traceparent", w3cHeader)
   └─> adapter.Set("tracestate", w3cState)

6. ProduceContextHeadersAdapter.Set
   └─> _setStringMethod.Invoke(headers, [name, value])  // Reflection call to MassTransit's Set method

7. MassTransit SendHeaders
   └─> Headers stored in outgoing message ✅
```

### Key Components

**ProduceContextHeadersAdapter** ([ContextPropagation.cs:110-297](ContextPropagation.cs#L110-L297))
- **Purpose:** Adapter to inject (write) trace headers into outgoing MassTransit messages
- **Method used:** Reflection via `MethodInfo.Invoke()`
- **Why reflection:** MassTransit's `SendHeaders.Set()` uses explicit interface implementation
- **Performance:** Cached `MethodInfo` lookup, single invoke per header

---

## Consumer Side: EXTRACTING Headers (Incoming Messages)

### Scenario
When a service **receives** a MassTransit message, we extract distributed tracing headers to continue the trace.

### Call Flow

```
1. MassTransit Application Code
   └─> Consumer receives message

2. DiagnosticObserver (MassTransit 7)
   ├─> MassTransitDiagnosticObserver.OnConsumeStart()
   └─> MassTransitCommon.ExtractTraceContext(tracer, consumeContext)

3. MassTransitCommon.ExtractTraceContext
   ├─> TryGetProperty<object>(context, "Headers")     // Reflection to get Headers property
   │   └─> Uses GetProperty() + GetInterfaces() to find Headers property
   │       Works for all context types including MessageConsumeContext (explicit interface impl)
   ├─> headers = context.Headers
   ├─> new ConsumeContextHeadersAdapter(headers)      // Create adapter for extraction
   │   └─> headers.DuckCast<IHeaders>()               // Duck typing to access GetAll() method
   │       Works because GetAll() uses implicit implementation (public method)
   └─> SpanContextPropagator.Extract(adapter)

4. SpanContextPropagator.Extract
   ├─> adapter.GetValues("x-datadog-trace-id")
   ├─> adapter.GetValues("x-datadog-parent-id")
   ├─> adapter.GetValues("x-datadog-sampling-priority")
   ├─> adapter.GetValues("traceparent")
   └─> adapter.GetValues("tracestate")

5. ConsumeContextHeadersAdapter.GetValues
   ├─> _headersProxy.GetAll()                         // Duck typing call to IHeaders.GetAll()
   ├─> foreach (headerValue in allHeaders)
   │   └─> headerValue.DuckCast<IHeaderValue>()       // Duck typing to read key/value
   └─> return matching header value

6. SpanContextPropagator
   └─> Reconstructs SpanContext from extracted headers ✅

7. Consumer Span Created
   └─> span.ParentId = extractedContext.SpanId        // Trace continues!
```

### Key Components

**ConsumeContextHeadersAdapter** ([ContextPropagation.cs:18-99](ContextPropagation.cs#L18-L99))
- **Purpose:** Adapter to extract (read) trace headers from incoming MassTransit messages
- **Method used:** Duck typing via `IHeaders.GetAll()` and `IHeaderValue`
- **Why duck typing:** Headers reading uses implicit implementation (public methods)
- **Performance:** Direct method calls, no reflection

---

## Why Different Approaches for Inject vs Extract?

### Injection (Producer) - Reflection Required

**MassTransit's SendHeaders implementation:**
```csharp
public class DictionarySendHeaders : ISendHeaders
{
    // EXPLICIT interface implementation - methods are PRIVATE
    void ISendHeaders.Set(string key, string value) { ... }
    void ISendHeaders.Set(string key, object value, bool overwrite) { ... }
}
```

- Methods are **private** on the class
- Only accessible through the interface
- Duck typing **cannot find** these methods
- **Requires reflection** using `GetInterfaceMap()`

### Extraction (Consumer) - Duck Typing Works

**MassTransit's Headers implementation:**
```csharp
public class JsonTransportHeaders : IHeaders
{
    // IMPLICIT implementation - method is PUBLIC
    public IEnumerable GetAll() { ... }
}
```

- Method is **public** on the class
- Accessible directly
- Duck typing **works**
- No reflection needed

---

## Context Type Variations

### For Property Access (Getting Headers from Context)

| Context Type | Has Headers? | Implementation | Access Method |
|--------------|--------------|----------------|---------------|
| `MessageConsumeContext<T>` | ✅ Yes | Explicit (on interface) | ❌ Duck fails → Use reflection |
| `CorrelationIdConsumeContextProxy<T>` | ✅ Yes | Implicit (public) | ✅ Duck works |
| `InMemorySagaConsumeContext<T,M>` | ✅ Yes | Implicit (public) | ✅ Duck works |

**Solution:** Use `TryGetProperty` (reflection) for reliability across all types.

### For Header Reading (Getting Values from Headers)

| Headers Type | Has GetAll()? | Implementation | Access Method |
|--------------|---------------|----------------|---------------|
| `JsonTransportHeaders` | ✅ Yes | Implicit (public) | ✅ Duck works |
| `DictionaryHeaders` | ✅ Yes | Implicit (public) | ✅ Duck works |

**Solution:** Duck typing works for all header types.

---

## Summary

### What Uses Duck Typing (Works)
- ✅ `ISendContext.Headers` - Getting Headers property from SendContext
- ✅ `IHeaders.GetAll()` - Reading headers from Headers collection
- ✅ `IHeaderValue` - Accessing individual header key/value pairs
- ✅ `IW3CActivity.TraceId` - Getting trace ID from Activity for exception tracking

### What Uses Reflection (Required)
- ❌ `ProduceContextHeadersAdapter.Set()` - Calling SendHeaders.Set() (explicit interface)
- ❌ `TryGetProperty("Headers")` - Getting Headers from MessageConsumeContext (explicit interface)
- ❌ `TryGetProperty("ReceiveContext", "InputAddress", etc.)` - DiagnosticObserver property access
- ❌ `GetMessageType()` - Generic type arguments
- ❌ `Exception.GetType()` - Runtime exception types

### Optimization Strategy
1. **Try duck typing first** where it might work (e.g., proxy types)
2. **Fall back to reflection** when duck typing fails (e.g., MessageConsumeContext)
3. **Cache reflection results** where possible (e.g., MethodInfo in ProduceContextHeadersAdapter)

---

## Files Overview

| File | Purpose |
|------|---------|
| **ContextPropagation.cs** | Contains ProduceContextHeadersAdapter (inject) and ConsumeContextHeadersAdapter (extract) |
| **MassTransitCommon.cs** | InjectTraceContext() and ExtractTraceContext() methods, TryGetProperty helper |
| **DuckTypes/ISendContext.cs** | Duck type for SendContext.Headers property |
| **DuckTypes/IHeaders.cs** | Duck type for Headers.GetAll() method |
| **DuckTypes/IHeaderValue.cs** | Duck type for HeaderValue.Key and Value properties |
| **DuckTypes/IConsumeContext.cs** | Duck type for ConsumeContext properties (ReceiveContext, MessageId, etc.) |
| **DuckTypes/IReceiveContext.cs** | Duck type for ReceiveContext.TransportHeaders and InputAddress |

---

## Diagram: Complete End-to-End Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                         SERVICE A (Producer)                     │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ bus.Send(message)
                              ↓
                    ┌──────────────────────┐
                    │  SendIntegration     │ (CallTarget intercepts)
                    └──────────────────────┘
                              │
                              ↓
              ┌───────────────────────────────────┐
              │ MassTransitCommon.InjectTraceContext │
              └───────────────────────────────────┘
                              │
                ┌─────────────┴─────────────┐
                │  ProduceContextHeadersAdapter │
                └────────────────────────────┘
                              │
                              │ Set headers via reflection
                              ↓
                    ┌──────────────────────┐
                    │ MassTransit Message  │
                    │ + Trace Headers      │
                    └──────────────────────┘
                              │
                              │ Message sent over network
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                         SERVICE B (Consumer)                     │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ Consumer receives message
                              ↓
                    ┌──────────────────────┐
                    │ ConsumeIntegration or│
                    │ DiagnosticObserver   │
                    └──────────────────────┘
                              │
                              ↓
              ┌───────────────────────────────────┐
              │ MassTransitCommon.ExtractTraceContext │
              │ (uses TryGetProperty reflection)  │
              └───────────────────────────────┘
                              │
                ┌─────────────┴─────────────┐
                │ ConsumeContextHeadersAdapter │
                │ (uses duck typing)        │
                └────────────────────────────┘
                              │
                              │ Extract headers
                              ↓
                    ┌──────────────────────┐
                    │  SpanContext         │
                    │  TraceId + ParentId  │
                    └──────────────────────┘
                              │
                              ↓
                      Consumer Span Created
                      with ParentId set ✅

                    Distributed Trace Continues!
```

---

## Quick Reference

**To inject headers (producer):**
```csharp
MassTransitCommon.InjectTraceContext(tracer, sendContext, scope);
// Uses: ProduceContextHeadersAdapter + reflection
```

**To extract headers (consumer):**
```csharp
var context = MassTransitCommon.ExtractTraceContext(tracer, consumeContext);
// Uses: reflection to get Headers, then ConsumeContextHeadersAdapter + duck typing
```

**Exception tracking:**
```csharp
var traceId = MassTransitCommon.ExtractTraceIdFromActivity(activity);
// Uses: IW3CActivity duck typing
```

---

## Related Documentation

- [WHY_DUCK_TYPING_FAILED.md](WHY_DUCK_TYPING_FAILED.md) - Detailed explanation of explicit vs implicit interface implementation
- [REFLECTION_USAGE_SUMMARY.md](REFLECTION_USAGE_SUMMARY.md) - Complete analysis of all reflection usage

## Date
2026-02-06
