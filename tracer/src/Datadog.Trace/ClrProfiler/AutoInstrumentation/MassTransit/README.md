# MassTransit Integration and Context Propagation

This document explains the MassTransit instrumentation approach and the context propagation changes made for RabbitMQ and AWS SNS/SQS to support proper distributed tracing when using messaging frameworks.

## Table of Contents

- [MassTransit 7.x Instrumentation](#masstransit-7x-instrumentation)
- [Context Propagation Changes](#context-propagation-changes)
- [Potential Issues and Considerations](#potential-issues-and-considerations)

---

## MassTransit 7.x Instrumentation

### Transport-Agnostic Approach

The MassTransit integration uses a **transport-agnostic** approach via the `IConfigureReceiveEndpoint` interface. This means a single hook point automatically instruments all MassTransit transports (RabbitMQ, Azure Service Bus, Amazon SQS, ActiveMQ, In-Memory, etc.).

### Key Files

| File | Purpose |
|------|---------|
| `FilterInjection/AddMassTransitIntegration.cs` | Hooks `AddMassTransit()` to register our `IConfigureReceiveEndpoint` implementation |
| `FilterInjection/DatadogConfigureReceiveEndpoint.cs` | Implements `IConfigureReceiveEndpoint` to inject filters into all receive endpoints |
| `FilterInjection/DatadogConsumePipeSpecification.cs` | Creates the consume filter specification for MassTransit's GreenPipes pipeline |
| `FilterInjection/DatadogConsumeFilter.cs` | The actual filter that creates "process" spans for consume operations |

### How It Works

1. `AddMassTransitIntegration` intercepts the `AddMassTransit()` call
2. In `OnMethodEnd`, it registers `DatadogConfigureReceiveEndpoint` as an `IConfigureReceiveEndpoint` service
3. MassTransit calls `Configure()` for every receive endpoint on all transports
4. We inject `DatadogConsumePipeSpecification` into the consume pipeline
5. When messages are consumed, `DatadogConsumeFilter.Send()` creates process spans

### Span Types

- **Receive spans**: Created by `ReceivePipeDispatcherIntegration` when messages arrive
- **Process spans**: Created by `DatadogConsumeFilter` when consumer logic executes
- **Publish spans**: Captured by underlying transport instrumentation (RabbitMQ, SNS, etc.)

---

## Context Propagation Changes

### The Problem

When using MassTransit (or other messaging frameworks) with underlying transports like RabbitMQ or AWS SNS/SQS, there are **two layers of instrumentation**:

1. **Framework layer** (MassTransit) - Creates spans for `Publish()` operations
2. **Transport layer** (RabbitMQ/SNS/SQS) - Creates spans for actual transport calls

Without proper context propagation, traces appear **disconnected**:

```
MassTransit Publish span (parent: request span)
     └── [DISCONNECTED]

RabbitMQ basic.publish span (new root span) ← Creates its own trace!
```

This happens because:
- MassTransit's `Publish()` may use **async batching** - it doesn't immediately call the transport
- By the time the transport's publish is called, the MassTransit span may already be closed
- There's no ambient span context, so the transport creates a **new root span**

### The Solution

**Extract trace context from message headers before creating transport spans.**

MassTransit and other frameworks inject trace context headers into messages **before** calling the underlying transport. The transport integration now:

1. Checks if headers already contain trace context
2. Extracts the context if present
3. Uses it as the parent for the transport span

### RabbitMQ Changes

**File**: `RabbitMQ/BasicPublishIntegration.cs`

```csharp
// Check if headers already contain trace context (e.g., injected by MassTransit)
PropagationContext extractedContext = default;
if (basicProperties.Headers is IDictionary<string, object> headers)
{
    extractedContext = tracer.TracerManager.SpanContextPropagator
                             .Extract(headers, default(ContextPropagation))
                             .MergeBaggageInto(Baggage.Current);
}

// Use extracted context as parent
var scope = RabbitMQIntegration.CreateScope(tracer, ..., context: extractedContext);
```

**File**: `RabbitMQ/ContextPropagation.cs`

The `Get()` method now handles both header formats:
- `byte[]` - Native RabbitMQ format
- `string` - Framework-injected format (before byte conversion)

```csharp
if (value is byte[] bytes) { return Encoding.UTF8.GetString(bytes); }
if (value is string str) { return str; }
```

### AWS SNS Changes

**File**: `AWS/SNS/AwsSnsHandlerCommon.cs`

```csharp
// Check if message attributes already contain trace context
ISpanContext? parentContext = null;
if (sendType == SendType.SingleMessage)
{
    var extractedContext = ContextPropagation.ExtractHeadersFromMessage(tracer, messageAttributesProxy);
    parentContext = extractedContext.SpanContext;
}

var scope = AwsSnsCommon.CreateScope(tracer, ..., parentContext);
```

**File**: `AWS/Shared/ContextPropagation.cs`

New `ExtractHeadersFromMessage()` method with dual extraction strategy:
1. **Primary**: Check for `_datadog` attribute containing JSON-formatted trace context
2. **Fallback**: Iterate through individual message attributes for trace headers

### Result: Connected Traces

After these changes:

```
MassTransit Publish span (parent: request span)
     └── RabbitMQ basic.publish span (parent: MassTransit span) ✓
```

---

## Potential Issues and Considerations

### 1. Double Span Creation

**Behavior**: Both MassTransit AND transport spans are created for publish operations.

**This is intentional**:
- MassTransit span = application-level publish (business logic)
- Transport span = transport-level send (actual network call)
- They provide different information (timing, errors at different layers)

Users can disable one integration if they prefer fewer spans.

### 2. Context Overwriting

**Behavior**: The transport integration re-injects its own trace context into headers after extracting.

**Impact**:
- The **child span's context** is injected (correct for downstream consumers)
- Consumers will see the transport span as parent, not the framework span
- This maintains the trace chain correctly

### 3. Amazon SQS/SNS Trace Connectivity

**Status**: ✅ Working - MassTransit receive/process spans are connected to the SNS producer span.

**How it works**: MassTransit publishes via SNS, which injects trace context into the SQS message. On the receive side:
1. MassTransit's `JsonTransportHeaders` doesn't expose the trace context for SQS
2. We fall back to extracting from the raw SQS message's `MessageAttributes`
3. The `_datadog` attribute contains JSON-encoded trace headers (as binary data)
4. We parse this JSON and extract the parent span context

**Note**: The trace context is stored as binary data in the SQS MessageAttribute, not as a string. Both String and Binary formats are supported.

### 4. Batch Operations (SNS)

**Limitation**: For batch publishes, context extraction is skipped.

**Reason**: Each message in a batch might have different parent contexts. Taking context from one message wouldn't be correct for all.

### 4. Performance Overhead

**Analysis**:
- Header dictionary lookup: O(1) average
- JSON deserialization (SNS): ~1-5μs for small payloads
- String encoding (RabbitMQ): ~100ns per header

Overhead is minimal compared to network I/O.

### 5. Silent Exception Handling

**Behavior**: Extraction errors are silently caught:

```csharp
catch
{
    // Ignore extraction errors, will create a new root span
}
```

**Impact**:
- Malformed headers won't crash the application
- Failed extractions aren't logged (could make debugging harder)

### 6. Header Format Compatibility

| Transport | Formats Supported |
|-----------|-------------------|
| RabbitMQ | `byte[]` (native), `string` (framework-injected) |
| SNS/SQS | `_datadog` JSON attribute, individual message attributes |

---

## Summary

| Aspect | Status | Notes |
|--------|--------|-------|
| **In-Memory Transport** | ✅ Working | MassTransit receive → process spans connected |
| **RabbitMQ Transport** | ✅ Working | Producer → consumer → MassTransit spans all connected |
| **SQS/SNS Transport** | ✅ Working | SNS producer → MassTransit receive → process spans all connected |
| **Double Spans** | ⚠️ Expected | Both framework and transport spans created |
| **Batch Operations** | ⚠️ Limited | SNS batch doesn't extract parent context |
| **Performance** | ✅ Minimal | Small overhead for header extraction |
| **Error Handling** | ⚠️ Silent | Extraction errors don't log |
| **Header Formats** | ✅ Compatible | Handles multiple header formats |
| **Context Flow** | ✅ Correct | Downstream gets child span context |

The changes are correct and necessary for proper distributed tracing when using messaging frameworks with transport-level instrumentation.
