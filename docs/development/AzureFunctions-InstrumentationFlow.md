# Azure Functions Instrumentation Flow

This document explains how the dd-trace-dotnet instrumentation works for Azure Functions, with a focus on **when** each method interception is triggered and how data (active scope, trace context) flows from one step to the next.

**Related Documentation:**
- [Azure Functions Integration Guide](AzureFunctions.md) - Setup, testing, and troubleshooting
- [Azure Functions Architecture Deep Dive](for-ai/AzureFunctions-Architecture.md) - Architectural details about host and worker processes

---

## Table of Contents

1. [Overview](#overview)
2. [Isolated Functions with ASP.NET Core Integration](#isolated-functions-with-aspnet-core-integration)
3. [Isolated Functions without ASP.NET Core Integration](#isolated-functions-without-aspnet-core-integration)
4. [In-Process Functions](#in-process-functions)
5. [Key Components](#key-components)
6. [Context Flow Diagrams](#context-flow-diagrams)

---

## Overview

Azure Functions instrumentation uses **CallTarget** to intercept specific methods in the Azure Functions runtime. The instrumentation must handle three execution models:

1. **Isolated Functions with ASP.NET Core Integration** (Most Important): User code runs in a separate worker process; HTTP requests are proxied directly to the worker with coordination via gRPC and `IHttpCoordinator`
2. **Isolated Functions without ASP.NET Core Integration**: User code runs in a separate worker process; all request data flows over gRPC
3. **In-Process Functions** (Deprecated): User code runs in the same process as the Functions host (being phased out by Microsoft)

The key challenge is ensuring that spans created in different processes (host and worker) are correctly parented to form a unified distributed trace.

---

## Isolated Functions with ASP.NET Core Integration

When the worker uses `ConfigureFunctionsWebApplication()` and references `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore`, the host uses **HTTP proxying** in addition to the gRPC invocation request.

### Key Differences

1. **gRPC message is still sent**: The `InvocationRequest` gRPC message is still sent to the worker, but the HTTP-specific data in `ToRpcHttp()` returns a nearly empty `TypedData` (see [Azure Functions Host source](https://github.com/Azure/azure-functions-host/blob/de87f37cec3cf02b3e29716764d4ceb6c2856fa8/src/WebJobs.Script.Grpc/MessageExtensions/GrpcMessageConversionExtensions.cs#L123-L125))
2. **HTTP request is also proxied**: In parallel, the host uses YARP (Yet Another Reverse Proxy) to forward the original HTTP request directly to the worker (see [GrpcWorkerChannel.cs:900-903](https://github.com/Azure/azure-functions-host/blob/dev/src/WebJobs.Script.Grpc/Channel/GrpcWorkerChannel.cs#L900-L903))
3. **Trace context flows via HTTP headers**: Not via gRPC message headers
4. **Both mechanisms are used**: The gRPC message triggers the function invocation context, while the HTTP request carries the actual request data

### Integration Points

#### 1. FunctionInvocationMiddleware.Invoke (HOST PROCESS)
Same as before - creates the HTTP span

---

#### 2. GrpcMessageConversionExtensions.ToRpcHttp (HOST PROCESS)
**When Triggered**: Same as before

**What It Does Differently**:
1. Checks if HTTP proxying is enabled by inspecting `GrpcCapabilities.GetCapabilityState("HttpUri")`
2. If proxying is enabled:
   - **Creates a new empty `TypedData`** instead of modifying the existing one (which is a shared singleton)
   - Still injects trace context into the new empty gRPC message (for consistency, though it's not used)
3. If proxying is disabled:
   - Injects trace context into the actual gRPC message (standard mode)

**Code Flow**:
```csharp
// tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/Isolated/GrpcMessageConversionExtensionsToRpcHttpIntegration.cs:77-91

var isHttpProxying = !string.IsNullOrEmpty(capabilities.GetCapabilityState("HttpUri"));
var requiresRouteParameters = !string.IsNullOrEmpty(capabilities.GetCapabilityState("RequiresRouteParameters"));

// When proxying, create a new TypedData; otherwise, use the provided one
var typedData = isHttpProxying && !requiresRouteParameters
                    ? TypedDataHelper<TReturn>.CreateTypedData()
                    : returnValue.DuckCast<ITypedData>();

// Inject context (goes into the new empty message when proxying)
var context = new PropagationContext(span.Context, Baggage.Current);
tracer.TracerManager.SpanContextPropagator.Inject(
    context,
    new RpcHttpHeadersCollection<TTarget>(typedData.Http, useNullableHeaders));

return (TReturn)typedData.Instance!;
```

**Key Insight**: In proxying mode, the injected trace context goes into an **empty gRPC message** that is effectively discarded. The real trace context flows through the **proxied HTTP request headers**.

---

#### 3. HTTP Proxying (HOST PROCESS)
**When**: Before the gRPC message is sent

**What Happens**:
1. The host's `GrpcWorkerChannel` initiates HTTP forwarding **first** (see [GrpcWorkerChannel.cs:897-908](https://github.com/Azure/azure-functions-host/blob/dev/src/WebJobs.Script.Grpc/Channel/GrpcWorkerChannel.cs#L897-L908))
2. The host's `DefaultHttpProxyService` adds a correlation header containing the invocation ID and starts YARP forwarding asynchronously (the task is not awaited)
3. **Then** the gRPC `InvocationRequest` message is sent to the worker
4. The host's **HTTP client instrumentation** (intercepting `SocketsHttpHandler`) automatically:
   - Creates a span for the HTTP call (operation name: `http.request`)
   - Injects trace context headers (`x-datadog-trace-id`, etc.) into the proxied request
5. The worker receives both the gRPC message and the HTTP request

**Host Code Flow**:
```csharp
// https://github.com/Azure/azure-functions-host/blob/dev/src/WebJobs.Script.Grpc/Channel/GrpcWorkerChannel.cs#L897-908

// If the worker supports HTTP proxying, ensure this request is forwarded PRIOR
// to sending the invocation request to the worker
if (IsHttpProxyingWorker && context.FunctionMetadata.IsHttpTriggerFunction())
{
    _httpProxyService.StartForwarding(context, _httpProxyEndpoint);
}

await SendStreamingMessageAsync(new StreamingMessage
{
    InvocationRequest = invocationRequest
});
```

```csharp
// https://github.com/Azure/azure-functions-host/blob/dev/src/WebJobs.Script/Http/DefaultHttpProxyService.cs#L100-107

// Add correlation header with invocation ID
httpRequest.Headers[ScriptConstants.HttpProxyCorrelationHeader] = context.ExecutionContext.InvocationId.ToString();

// Start forwarding task (async, not awaited - this runs in parallel with gRPC)
var forwardingTask = _httpForwarder.SendAsync(httpContext, httpUri.ToString(),
    _messageInvoker, _forwarderRequestConfig, _httpTransformer).AsTask();

context.Properties[ScriptConstants.HttpProxyTask] = forwardingTask;
```

**Instrumentation Involved**:
- Not specific to Azure Functions
- Standard HTTP client instrumentation in dd-trace-dotnet
- Intercepts `SocketsHttpHandler.SendAsync()` or similar

**Active Scope During Proxying**: HTTP span is still active in the host

**Trace Context Injection**: Happens automatically via HTTP client instrumentation

**Coordination**: The invocation ID is used as a correlation key between the gRPC message and the HTTP request

---

#### 4. Worker Receives Both gRPC and HTTP (WORKER PROCESS)

The worker receives two concurrent messages:
1. **gRPC InvocationRequest** message
2. **HTTP request** (proxied via YARP)

Both are correlated using the **invocation ID**.

**Coordination Mechanism**:

The `IHttpCoordinator` interface synchronizes the gRPC and HTTP pathways:

```csharp
// https://github.com/Azure/azure-functions-dotnet-worker/blob/main/extensions/Worker.Extensions.Http.AspNetCore/src/FunctionsMiddleware/FunctionsHttpProxyingMiddleware.cs#L33-69

// This middleware runs on the gRPC function execution path
public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
{
    var invocationId = context.InvocationId;

    // BLOCKS here until the HTTP request arrives with matching invocation ID
    var httpContext = await _coordinator.SetFunctionContextAsync(invocationId, context);

    // Now we have both gRPC FunctionContext and HTTP HttpContext
    AddHttpContextToFunctionContext(context, httpContext);

    try
    {
        await next(context); // Execute the function

        // ... handle response ...
    }
    finally
    {
        // Signal to HTTP handler that we're done
        _coordinator.CompleteFunctionInvocation(invocationId);
    }
}
```

```csharp
// https://github.com/Azure/azure-functions-dotnet-worker/blob/main/extensions/Worker.Extensions.Http.AspNetCore/src/AspNetMiddleware/FunctionsHttpContextExtensions.cs#L13-18

// This runs on the ASP.NET Core HTTP pipeline
internal static Task InvokeFunctionAsync(this HttpContext context)
{
    var coordinator = context.RequestServices.GetRequiredService<IHttpCoordinator>();

    // Extract invocation ID from correlation header
    context.Request.Headers.TryGetValue(Constants.CorrelationHeader, out StringValues invocationId);

    // BLOCKS here until the gRPC handler signals completion
    return coordinator.RunFunctionInvocationAsync(invocationId!);
}
```

**Coordination Flow**:
1. **gRPC message arrives first** (usually): Calls `SetFunctionContextAsync(invocationId, context)` - **BLOCKS** waiting for HTTP request
2. **HTTP request arrives** with matching invocation ID in header: Calls `RunFunctionInvocationAsync(invocationId)` - **BLOCKS** waiting for function completion
3. **Coordinator matches them** by invocation ID and unblocks the gRPC handler
4. **Function executes** with access to both gRPC `FunctionContext` and HTTP `HttpContext`
5. **When function completes**: gRPC handler calls `CompleteFunctionInvocation(invocationId)`
6. **HTTP handler unblocks** and can send the response back to the client

**Why Both Are Needed**:
- **gRPC message** provides: Function metadata, bindings configuration, execution context
- **HTTP request** provides: Actual HTTP request data (headers, body, query params)

**HTTP Request Handling**:
The proxied HTTP request arrives at the worker's ASP.NET Core pipeline, but **NO span is created automatically**.

**Key Point**: `AspNetCoreDiagnosticObserver` is **disabled** in Azure Functions workers (see `Instrumentation.cs:473-489`). Even though the worker is an ASP.NET Core application, the diagnostic observer that would normally create `aspnet_core.request` spans is not active.

This means the HTTP request flows through the ASP.NET Core pipeline without automatic instrumentation, and the only span created is in the next step by `FunctionExecutionMiddleware`

---

#### 5. FunctionExecutionMiddleware.Invoke (WORKER PROCESS)
**When Triggered**: After `IHttpCoordinator` has matched up the gRPC invocation with the HTTP request

**What It Does**:
1. Checks if there is an active scope - there is **NO active scope** because AspNetCoreDiagnosticObserver is disabled
2. Extracts trace context from the `HttpContext` that was added to the `FunctionContext` by the coordinator
3. Creates a **root span** for the function execution, with the extracted parent context

**Code Path**:
Same as standard isolated mode:

```csharp
// In CreateIsolatedFunctionScope():

// For HTTP triggers, extract propagated context from the FunctionContext
// (which now contains the HttpContext added by the coordinator)
if (triggerType == "Http")
{
    extractedContext = ExtractPropagatedContextFromHttp(context, entry.Key as string)
        .MergeBaggageInto(Baggage.Current);
}

// InternalActiveScope is null because AspNetCoreDiagnosticObserver is disabled
if (tracer.InternalActiveScope == null)
{
    // THIS PATH is taken in ASP.NET Core integration mode
    scope = tracer.StartActiveInternal(OperationName, tags: tags, parent: extractedContext.SpanContext);
}
```

**Key Detail**: `ExtractPropagatedContextFromHttp()` extracts the trace context from the HTTP request headers (which were added by the host's HTTP client instrumentation when it proxied the request). The function span is correctly parented to the host's HTTP span.

**Important**: The trace context flows from:
1. Host's `azure_functions.invoke` span → HTTP client instrumentation adds headers
2. HTTP headers → Worker's `IHttpCoordinator` adds `HttpContext` to `FunctionContext`
3. `FunctionContext.HttpContext.Request.Headers` → Extracted by `ExtractPropagatedContextFromHttp()`
4. Extracted context → Used as parent for worker's `azure_functions.invoke` span

---

### Isolated HTTP Trigger Flow (ASP.NET Core Integration)

```
Client
  ↓ HTTP Request (with traceparent: 00-TID-PID-01)
Host Process (func.exe)
  ↓
  1. FunctionInvocationMiddleware.Invoke ← INSTRUMENTED (HOST)
     - Extract context from HTTP headers (trace_id=TID, parent_id=PID)
     - Create span: azure_functions.invoke (span_id=H1, parent_id=PID, trace_id=TID)
     - Active scope (HOST): Function span
  ↓
  2. GrpcMessageConversionExtensions.ToRpcHttp ← INSTRUMENTED (HOST)
     - Detect HTTP proxying is enabled
     - Create empty gRPC message (not used for trace context)
     - Active scope (HOST): HTTP span
  ↓
  3. FunctionExecutor.TryExecuteAsync ← INSTRUMENTED (HOST)
     - Update Function span with function name
     - Active scope (HOST): Function span
  ↓
  4. GrpcWorkerChannel.SendInvocationRequest (HOST)
     - Both happen in parallel (see GrpcWorkerChannel.cs:900-903):
       A. Send InvocationRequest gRPC message to worker (triggers function context)
       B. HTTP Proxying - DefaultHttpProxyService forwards original HTTP request
     - HTTP client instrumentation ← INSTRUMENTED (HOST)
       - Create span: http.request (span_id=H2, parent_id=H1, trace_id=TID)
       - Inject context into proxied HTTP headers (trace_id=TID, parent_id=H2)
     - Active scope (HOST): HTTP client span (nested under HTTP span)
     ↓
Worker Process (dotnet MyApp.dll)
  ↓
  5. Receives BOTH:
     - InvocationRequest gRPC message (provides function invocation context)
     - Proxied HTTP request (provides actual HTTP request data)
  ↓
  6. ASP.NET Core receives proxied HTTP request
     - NO instrumentation fires (AspNetCoreDiagnosticObserver is DISABLED)
     - HTTP request flows through ASP.NET Core pipeline without creating a span
     - Active scope (WORKER): null
  ↓
  7. FunctionExecutionMiddleware.Invoke ← INSTRUMENTED (WORKER)
     - Triggered by InvocationRequest gRPC message
     - No active scope (AspNetCoreDiagnosticObserver disabled)
     - Extract context from proxied HTTP request headers (trace_id=TID, parent_id=H2)
     - Create span: azure_functions.invoke (span_id=W1, parent_id=H2, trace_id=TID)
     - Active scope (WORKER): Function span
  ↓
  8. User function executes
     - Can access HttpRequest from proxied HTTP request
     - Can create additional spans parented to function span
  ↓
  9. FunctionExecutionMiddleware.Invoke completes
     - Close function span
     - Send HTTP response (via HTTP proxy)
     - Send InvocationResponse gRPC message
  ↓
Host Process
  ↓
  11. HTTP Proxying completes
      - Close HTTP client span
  ↓
  12. FunctionExecutor.TryExecuteAsync completes
      - No span to close
  ↓
  13. FunctionInvocationMiddleware.Invoke completes
      - Close HTTP span
  ↓
Response sent to client
```

**Resulting Distributed Trace**:
```
trace_id: TID

Host spans (serialized by host PID):
├─ azure_functions.invoke (s_id: H1, p_id: PID, t_id: TID)    [HOST, tag: aas.function.process=host]
   └─ http.request (s_id: H2, p_id: H1, t_id: TID)         [HOST, tag: aas.function.process=host]

Worker spans (serialized by worker PID):
      └─ azure_functions.invoke (s_id: W1, p_id: H2, t_id: TID) [WORKER, tag: aas.function.process=worker]
         └─ ... (user-created spans)
```

**Key Difference from Standard Mode**: There is no `aspnet_core.request` span in the worker because `AspNetCoreDiagnosticObserver` is disabled. The function span is directly parented to the host's `http.request` span.

---

## Isolated Functions without ASP.NET Core Integration

In this mode, all request data flows from host to worker over gRPC.

### Integration Points

#### 1. FunctionInvocationMiddleware.Invoke (HOST PROCESS)
**When Triggered**: When the host receives an HTTP request

**What It Does**: Same as in-process - creates the outer HTTP span

**Active Scope After**: HTTP span is active in the host process

---

#### 2. GrpcMessageConversionExtensions.ToRpcHttp (HOST PROCESS)
**File**: `GrpcMessageConversionExtensionsToRpcHttpIntegration.cs`
**Intercepted Method**: `Microsoft.Azure.WebJobs.Script.Grpc.GrpcMessageConversionExtensions.ToRpcHttp(HttpRequest, ILogger, GrpcCapabilities)`
**Assembly**: `Microsoft.Azure.WebJobs.Script.Grpc`

**When Triggered**:
- Called when the host converts an HTTP request into a gRPC message (`RpcHttp`)
- Occurs **after** `FunctionInvocationMiddleware.Invoke` has created the HTTP span
- This gRPC message will be sent to the worker process

**What It Does**:
1. **Injects trace context** from the active scope into the gRPC message headers
2. **Overwrites any existing trace context** in the original HTTP request
3. Ensures the worker process can extract the correct parent context

**Why This Is Critical**:
- The incoming HTTP request may already have distributed tracing headers from the original client
- We need to **replace** those headers with the span ID of the HTTP span created in the host
- This ensures the worker's span becomes a **child** of the host's span, not a sibling or separate trace

**Code Flow**:
```csharp
// tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/Isolated/GrpcMessageConversionExtensionsToRpcHttpIntegration.cs:40-92

internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(
    TTarget nullInstance, TReturn returnValue, Exception? exception, in CallTargetState state)
{
    // Check if we have an active Azure Functions span
    if (tracer.ActiveScope is not Scope { Span: { OperationName: "azure_functions.invoke" } span })
    {
        return returnValue;
    }

    // Get or create the TypedData with RpcHttp
    var typedData = returnValue.DuckCast<ITypedData>();

    // Inject trace context into gRPC message headers
    var context = new PropagationContext(span.Context, Baggage.Current);
    tracer.TracerManager.SpanContextPropagator.Inject(
        context,
        new RpcHttpHeadersCollection<TTarget>(typedData.Http, useNullableHeaders));

    return (TReturn)typedData.Instance!;
}
```

**Active Scope After**: Still the HTTP span in the host process

**Context Propagation**:
- Datadog trace headers (`x-datadog-trace-id`, `x-datadog-parent-id`, `x-datadog-sampling-priority`) are **injected** into the `RpcHttp.Headers` dictionary
- These headers will be available to the worker process when it receives the gRPC message

---

#### 3. FunctionExecutor.TryExecuteAsync (HOST PROCESS)
**When Triggered**: When the host is about to invoke the "proxy" function that forwards to the worker

**What It Does**:
- For isolated functions, **does not create a new span**
- Instead, **updates the existing HTTP span** with function details
- The function name here is the "proxy" function (prefixed with `Functions.`)

---

#### 4. FunctionExecutionMiddleware.Invoke (WORKER PROCESS)
**File**: `FunctionExecutionMiddlewareInvokeIntegration.cs`
**Intercepted Method**: `Microsoft.Azure.Functions.Worker.Pipeline.FunctionExecutionMiddleware.Invoke(FunctionContext)`
**Assembly**: `Microsoft.Azure.Functions.Worker.Core`

**When Triggered**:
- Called when the **worker process** is about to execute the actual user function
- Part of the Azure Functions worker middleware pipeline
- Receives a `FunctionContext` with all invocation details

**What It Does**:
1. **Extracts trace context** from the gRPC message (which is embedded in `FunctionContext`)
2. **Creates a new span** representing the function execution in the worker process
3. This span should be **parented** to the host's HTTP span (using trace context from step 2)

**Code Flow**:
```csharp
// tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/Isolated/FunctionExecutionMiddlewareInvokeIntegration.cs:29-32

internal static CallTargetState OnMethodBegin<TTarget, TFunctionContext>(
    TTarget instance, TFunctionContext functionContext)
    where TFunctionContext : IFunctionContext
{
    return AzureFunctionsCommon.OnIsolatedFunctionBegin(functionContext);
}
```

This calls `AzureFunctionsCommon.CreateIsolatedFunctionScope()`:

```csharp
// tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/AzureFunctionsCommon.cs:200-294

internal static Scope? CreateIsolatedFunctionScope<T>(Tracer tracer, T context)
    where T : IFunctionContext
{
    // Determine trigger type from function bindings
    var triggerType = "Unknown";
    PropagationContext extractedContext = default;

    foreach (DictionaryEntry entry in context.FunctionDefinition.InputBindings)
    {
        var binding = entry.Value.DuckCast<BindingMetadata>();
        var type = binding.BindingType;

        triggerType = type switch
        {
            "httpTrigger" => "Http",
            "timerTrigger" => "Timer",
            "serviceBusTrigger" => "ServiceBus",
            // ... more trigger types
        };

        // For HTTP triggers, extract propagated context from the gRPC message
        if (triggerType == "Http")
        {
            extractedContext = ExtractPropagatedContextFromHttp(context, entry.Key as string)
                .MergeBaggageInto(Baggage.Current);
        }
        // For messaging triggers, extract from message properties
        else if (triggerType == "ServiceBus")
        {
            extractedContext = ExtractPropagatedContextFromMessaging(context, "UserProperties", ...);
        }

        break;
    }

    var functionName = context.FunctionDefinition.Name;

    // Create a new span with the extracted parent context
    var tags = new AzureFunctionsTags { TriggerType = triggerType, ShortName = functionName, ... };

    if (tracer.InternalActiveScope == null)
    {
        // Root span in worker process (but has a parent in host process via distributed context)
        scope = tracer.StartActiveInternal(OperationName, tags: tags, parent: extractedContext.SpanContext);
    }
    else
    {
        // Nested span (shouldn't normally happen in isolated mode)
        scope = tracer.StartActiveInternal(OperationName);
        AzureFunctionsTags.SetRootSpanTags(scope.Root.Span, functionName, ...);
    }

    scope.Span.ResourceName = $"{triggerType} {functionName}";
    return scope;
}
```

**Key Detail**: `ExtractPropagatedContextFromHttp()` navigates the gRPC binding data:

```csharp
// tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/AzureFunctionsCommon.cs:296-345

private static PropagationContext ExtractPropagatedContextFromHttp<T>(T context, string? bindingName)
{
    // Navigate to IFunctionBindingsFeature in context.Features
    object? feature = null;
    foreach (var keyValuePair in context.Features)
    {
        if (keyValuePair.Key.FullName?.Equals("Microsoft.Azure.Functions.Worker.Context.Features.IFunctionBindingsFeature") == true)
        {
            feature = keyValuePair.Value;
            break;
        }
    }

    var bindingFeature = feature.DuckCast<FunctionBindingsFeatureStruct>();

    // Get the InputData for the HTTP binding
    var requestDataObject = bindingFeature.InputData[bindingName!];
    var httpRequest = requestDataObject.DuckCast<HttpRequestDataStruct>();

    // Extract trace context from HTTP headers
    return Tracer.Instance.TracerManager.SpanContextPropagator.Extract(
        new HttpHeadersCollection(httpRequest.Headers));
}
```

**Active Scope After**: New function span is active in the worker process

**Distributed Trace Context**:
- Worker span's `parent_id` = Host HTTP span's `span_id`
- Worker span's `trace_id` = Host HTTP span's `trace_id`
- Both spans are in the **same trace**, forming a parent-child relationship across processes

---

### Isolated HTTP Trigger Flow (Standard Mode)

```
Client
  ↓ HTTP Request (with traceparent: 00-TID-PID-01)
Host Process (func.exe)
  ↓
  1. FunctionInvocationMiddleware.Invoke ← INSTRUMENTED (HOST)
     - Extract context from HTTP headers (trace_id=TID, parent_id=PID)
     - Create span: azure_functions.invoke (span_id=H1, parent_id=PID, trace_id=TID)
     - Active scope (HOST): Function span
  ↓
  2. GrpcMessageConversionExtensions.ToRpcHttp ← INSTRUMENTED (HOST)
     - Inject context into gRPC message headers
     - Headers now contain: trace_id=TID, parent_id=H1
     - Active scope (HOST): HTTP span
  ↓
  3. FunctionExecutor.TryExecuteAsync ← INSTRUMENTED (HOST)
     - Update Function span with function name
     - Active scope (HOST): Function span
  ↓
  4. gRPC message sent to worker
     ↓
Worker Process (dotnet MyApp.dll)
  ↓
  5. FunctionExecutionMiddleware.Invoke ← INSTRUMENTED (WORKER)
     - Extract context from gRPC message (trace_id=TID, parent_id=H1)
     - Create span: azure_functions.invoke (span_id=W1, parent_id=H1, trace_id=TID)
     - Active scope (WORKER): Function span
  ↓
  6. User function executes
     - Can create additional spans parented to function span
  ↓
  7. FunctionExecutionMiddleware.Invoke completes
     - Close function span
     - Send gRPC response
  ↓
Host Process
  ↓
  8. FunctionExecutor.TryExecuteAsync completes
     - No span to close (didn't create one)
  ↓
  9. FunctionInvocationMiddleware.Invoke completes
     - Close HTTP span
  ↓
Response sent to client
```

**Resulting Distributed Trace**:
```
trace_id: TID

Host spans (serialized by host PID):
├─ azure_functions.invoke (s_id: H1, p_id: PID, t_id: TID)  [HOST, tag: aas.function.process=host]

Worker spans (serialized by worker PID):
└─ azure_functions.invoke (s_id: W1, p_id: H1, t_id: TID) [WORKER, tag: aas.function.process=worker]
   └─ ... (user-created spans)
```

**Important**: The host and worker send spans to the Datadog Agent **independently**. The Agent receives two separate payloads:
1. Host process serializes and sends spans with `aas.function.process: host`
2. Worker process serializes and sends spans with `aas.function.process: worker`

The Datadog backend stitches them into a unified trace using `trace_id` and `parent_id` relationships.

---

## In-Process Functions

### Integration Points

#### 1. FunctionInvocationMiddleware.Invoke (HOST PROCESS)
**File**: `FunctionInvocationMiddlewareInvokeIntegration.cs`
**Intercepted Method**: `Microsoft.Azure.WebJobs.Script.WebHost.Middleware.FunctionInvocationMiddleware.Invoke(HttpContext)`
**Assembly**: `Microsoft.Azure.WebJobs.Script.WebHost`

**When Triggered**:
- Called when an HTTP request is received by the Azure Functions host
- Part of the ASP.NET Core middleware pipeline
- Executes **before** the function is invoked

**What It Does**:
1. **Extracts trace context** from incoming HTTP headers
2. **Creates the "outer" span** representing the HTTP request (`aspnet_core.request`)
3. Stores the scope in `CallTargetState` and stores `HttpContext` in state for later use
4. Does **not** have function-specific details yet (function name, trigger type, etc.)

**Code Flow**:
```csharp
// tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/FunctionInvocationMiddlewareInvokeIntegration.cs:43-57

internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, HttpContext httpContext)
{
    var scope = AspNetCoreRequestHandler.StartAspNetCorePipelineScope(
        tracer, security, httpContext, resourceName: null);

    if (scope != null)
    {
        return new CallTargetState(scope, state: httpContext);
    }

    return CallTargetState.GetDefault();
}
```

**Active Scope After**:
- `Tracer.InternalActiveScope` = New HTTP span created by this integration
- Span operation name: `azure_functions.invoke` (set by handler)
- Resource name: `GET /api/trigger` (default, will be updated later)

**Context Storage**:
- Scope stored in `CallTargetState.Scope`
- `HttpContext` stored in `CallTargetState.State`

---

#### 2. FunctionExecutor.TryExecuteAsync (HOST PROCESS)
**File**: `AzureFunctionsExecutorTryExecuteAsyncIntegration.cs`
**Intercepted Method**: `Microsoft.Azure.WebJobs.Host.Executors.FunctionExecutor.TryExecuteAsync(IFunctionInstance, CancellationToken)`
**Assembly**: `Microsoft.Azure.WebJobs.Host`

**When Triggered**:
- Called when the Functions host is about to execute a specific function
- Provides access to `IFunctionInstance` with function metadata

**What It Does**:
1. **Enriches the existing HTTP span** with function-specific details
2. For in-process functions, creates a **nested span** for the function execution
3. For isolated functions (where there's already an active scope from the HTTP request), it **updates the root span** instead of creating a nested span

**Code Flow**:
```csharp
// tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/AzureFunctionsExecutorTryExecuteAsyncIntegration.cs:40-43

internal static CallTargetState OnMethodBegin<TTarget, TFunction>(
    TTarget instance, TFunction functionInstance, CancellationToken cancellationToken)
    where TFunction : IFunctionInstance
{
    return AzureFunctionsCommon.OnFunctionExecutionBegin(instance, functionInstance);
}
```

This calls into `AzureFunctionsCommon.CreateScope()`:

```csharp
// tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/AzureFunctionsCommon.cs:57-180

internal static Scope? CreateScope<TFunction>(Tracer tracer, TFunction instanceParam)
{
    // Determine trigger type from binding source
    var triggerType = instanceParam.Reason switch
    {
        AzureFunctionsExecutionReason.HostCall => "Http",  // or "EventGrid"
        AzureFunctionsExecutionReason.AutomaticTrigger => "Timer", // or "ServiceBus", "Blob", etc.
        AzureFunctionsExecutionReason.Dashboard => "Dashboard",
        _ => "Unknown"
    };

    var functionName = instanceParam.FunctionDescriptor.ShortName;

    // For ISOLATED functions, update the root span instead of creating a new one
    if (tracer.Settings.AzureAppServiceMetadata is { IsIsolatedFunctionsApp: true }
        && tracer.InternalActiveScope is { } activeScope)
    {
        var rootSpan = activeScope.Root.Span;
        // Strip "Functions." prefix from isolated function names
        var remoteFunctionName = functionName?.StartsWith("Functions.") == true
                                    ? functionName.Substring(10)
                                    : functionName;

        AzureFunctionsTags.SetRootSpanTags(rootSpan, remoteFunctionName, ...);
        rootSpan.Type = SpanType;
        return null;  // Don't create a new scope
    }

    // For IN-PROCESS functions, create or update a span
    if (tracer.InternalActiveScope == null)
    {
        // Root span (non-HTTP triggers like Timer)
        scope = tracer.StartActiveInternal(OperationName, tags: tags);
    }
    else
    {
        // Nested span (HTTP triggers)
        scope = tracer.StartActiveInternal(OperationName);
        AzureFunctionsTags.SetRootSpanTags(scope.Root.Span, functionName, ...);
    }

    scope.Span.ResourceName = $"{triggerType} {functionName}";
    return scope;
}
```

**Active Scope After**:
- **In-Process, HTTP Trigger**: A new nested scope is active, child of the HTTP span
- **In-Process, Timer Trigger**: A new root scope is created (no parent)
- **Isolated Functions**: No new scope; existing scope is updated with function details

**Context Storage**:
- Scope stored in `CallTargetState.Scope`

---

### AspNetCoreDiagnosticObserver and Azure Functions

**Critical**: The `AspNetCoreDiagnosticObserver` (which subscribes to `Microsoft.AspNetCore.Hosting.HttpRequestIn.Start` diagnostic events) is **completely disabled** in Azure Functions environments.

This happens in `Instrumentation.cs:473-489`, where we check for the presence of `FUNCTIONS_EXTENSION_VERSION` and `FUNCTIONS_WORKER_RUNTIME` environment variables. If both are present, we **skip adding the AspNetCoreDiagnosticObserver** to the list of diagnostic observers:

```csharp
if (!string.IsNullOrEmpty(functionsExtensionVersion) && !string.IsNullOrEmpty(functionsWorkerRuntime))
{
    // Not adding the `AspNetCoreDiagnosticObserver` is particularly important for in-process Azure Functions.
    // The AspNetCoreDiagnosticObserver will be loaded in a separate Assembly Load Context, breaking the connection of AsyncLocal.
    // This is because user code is loaded within the functions host in a separate context.
    // Even in isolated functions, we don't want the AspNetCore spans to be created.
    Log.Debug("Skipping AspNetCoreDiagnosticObserver in Azure Functions.");
}
```

**Why disabled?**
- **In-process functions**: The diagnostic observer would be loaded in a separate Assembly Load Context, breaking AsyncLocal context flow
- **Isolated functions**: We want precise control over span creation using CallTarget instrumentation
- Both the **host process** and **worker process** have these environment variables set, so the observer is disabled in both

Instead, we use **CallTarget instrumentation** of `FunctionInvocationMiddleware.Invoke` (host) and `FunctionExecutionMiddleware.Invoke` (worker) to create spans with precise control.

---

### In-Process HTTP Trigger Flow

```
Client
  ↓ HTTP Request
Host Process (func.exe / Microsoft.Azure.WebJobs.Script.WebHost)
  ↓
  1. FunctionInvocationMiddleware.Invoke ← INSTRUMENTED (CallTarget)
     - Extract trace context from HTTP headers
     - Create span: azure_functions.invoke (ROOT) with operation name from AzureFunctionsCommon.OperationName
     - Active scope: HTTP/Function span
     - Note: Uses AspNetCoreHttpRequestHandler but with operation name "azure_functions.invoke", NOT "aspnet_core.request"
  ↓
  2. FunctionExecutor.TryExecuteAsync ← INSTRUMENTED
     - Access function metadata (name, trigger type)
     - Create nested span: azure_functions.invoke (CHILD of HTTP span)
     - Update root HTTP span with function details
     - Active scope: Function span
  ↓
  3. User function executes
     - Can create additional spans parented to function span
  ↓
  4. FunctionExecutor.TryExecuteAsync completes
     - Close function span
     - Active scope reverts to: HTTP span
  ↓
  5. FunctionInvocationMiddleware.Invoke completes
     - Close HTTP span
     - Active scope reverts to: null
  ↓
Response sent to client
```

**Resulting Trace Structure**:
```
azure_functions.invoke (s_id: A, p_id: null)      ← Root span (HTTP trigger)
└─ azure_functions.invoke (s_id: B, p_id: A)      ← Function execution span
   └─ ... (user-created spans)
```

**Note**: Both spans have the operation name `azure_functions.invoke`, but the root span represents the HTTP request and the nested span represents the function execution.

---

### In-Process Timer Trigger Flow

```
Azure Functions Timer
  ↓
Host Process
  ↓
  1. FunctionExecutor.TryExecuteAsync ← INSTRUMENTED
     - No existing active scope (no HTTP request)
     - Create root span: azure_functions.invoke (ROOT)
     - Active scope: Function span
  ↓
  2. User function executes
     - Can create additional spans parented to function span
  ↓
  3. FunctionExecutor.TryExecuteAsync completes
     - Close function span
     - Active scope reverts to: null
```

**Resulting Trace Structure**:
```
azure_functions.invoke (s_id: X, p_id: null)      ← Root span
└─ ... (user-created spans)
```

---

## Key Components

### 1. AspNetCoreHttpRequestHandler
**File**: `tracer/src/Datadog.Trace/PlatformHelpers/AspNetCoreHttpRequestHandler.cs`

**Purpose**: Centralized handler for creating and closing ASP.NET Core request spans

**Key Methods**:
- `StartAspNetCorePipelineScope()`: Creates a span for an incoming HTTP request
  - Extracts propagated context from HTTP headers
  - Creates a new scope with operation name `aspnet_core.request`
  - Stores the scope in `HttpContext.Features` via `RequestTrackingFeature`
- `StopAspNetCorePipelineScope()`: Closes the HTTP request span
  - Updates resource name and HTTP status code
  - Handles exceptions

**When Used**:
- Called by `FunctionInvocationMiddlewareInvokeIntegration` (Azure Functions)
- Also used by standard ASP.NET Core instrumentation

---

### 2. AzureFunctionsCommon
**File**: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/AzureFunctionsCommon.cs`

**Purpose**: Shared logic for creating Azure Functions spans

**Key Methods**:
- `OnFunctionExecutionBegin()`: Entry point for in-process function execution
- `CreateScope()`: Creates or updates spans for in-process functions
- `OnIsolatedFunctionBegin()`: Entry point for isolated function execution
- `CreateIsolatedFunctionScope()`: Creates spans for isolated functions
- `ExtractPropagatedContextFromHttp()`: Extracts trace context from gRPC message
- `ExtractPropagatedContextFromMessaging()`: Extracts trace context from messaging triggers

**Trigger Type Detection**:
- In-process: Uses `IFunctionInstance.Reason` and `BindingSource` type name
- Isolated: Uses `FunctionDefinition.InputBindings` and `BindingMetadata.BindingType`

---

### 3. RpcHttpHeadersCollection
**File**: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/Isolated/RpcHttpHeadersCollection.cs`

**Purpose**: Adapter for injecting trace context into gRPC `RpcHttp.Headers`

**Key Behavior**:
- Implements `IHeadersCollection` interface required by `SpanContextPropagator`
- Wraps the `RpcHttp.Headers` dictionary (from gRPC protobuf)
- Handles both nullable and non-nullable header value types

---

### 4. HttpHeadersCollection (Isolated)
**File**: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/Isolated/HttpHeadersCollection.cs`

**Purpose**: Adapter for extracting trace context from worker's `HttpRequestData.Headers`

**Key Behavior**:
- Implements `IHeadersCollection` interface
- Wraps the `IEnumerable<KeyValuePair<string, IEnumerable<string>>>` headers from gRPC binding data
- Used when extracting context in the worker process

---

### 5. AspNetCoreDiagnosticObserver
**File**: `tracer/src/Datadog.Trace/DiagnosticListeners/AspNetCoreDiagnosticObserver.cs`

**Purpose**: Subscribes to ASP.NET Core diagnostic events for automatic instrumentation

**Key Events**:
- `Microsoft.AspNetCore.Hosting.HttpRequestIn.Start`: Create span for incoming request
- `Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop`: Close span
- `Microsoft.AspNetCore.Diagnostics.UnhandledException`: Handle exceptions
- MVC events for route template extraction

**When Used**:
- Automatically enabled for all ASP.NET Core apps
- In Azure Functions with ASP.NET Core integration, it creates spans in the worker process

---

## Context Flow Diagrams

### Isolated HTTP Trigger (ASP.NET Core Integration)

```
┌──────────────────────────────────────────────────────────────────┐
│                         Host Process                              │
├──────────────────────────────────────────────────────────────────┤
│  1. FunctionInvocationMiddleware.Invoke                           │
│     [FunctionInvocationMiddlewareInvokeIntegration.OnMethodBegin] │
│     ┌────────────────────────────────────────┐                   │
│     │ Extract: HTTP headers (W3C/Datadog)    │                   │
│     │ Create:  azure_functions.invoke (H1)   │                   │
│     │ Store:   Scope in CallTargetState      │                   │
│     └────────────────────────────────────────┘                   │
│                     │                                              │
│                     │ Active: azure_functions.invoke (H1)        │
│                     ↓                                              │
│  2. GrpcMessageConversionExtensions.ToRpcHttp                     │
│     [GrpcMessageConversionExtensionsToRpcHttpIntegration]         │
│     ┌────────────────────────────────────────┐                   │
│     │ Detect:  HttpUri capability enabled    │                   │
│     │ Create:  Empty TypedData (no context)  │                   │
│     │ Active:  azure_functions.invoke (H1)   │                   │
│     └────────────────────────────────────────┘                   │
│                     │                                              │
│                     ↓                                              │
│  3. HTTP Proxying via YARP                                        │
│     [Standard HTTP client instrumentation]                        │
│     ┌────────────────────────────────────────┐                   │
│     │ Forward: Original HTTP to worker       │                   │
│     │ Create:  http.request (H2, parent=H1)  │                   │
│     │ Inject:  trace_id, parent_id=H2        │                   │
│     │          into HTTP headers             │                   │
│     └────────────────────────────────────────┘                   │
│                     │                                              │
│                     ↓ HTTP request with headers                  │
└─────────────────────┼──────────────────────────────────────────┘
                      │
                      ↓
┌──────────────────────────────────────────────────────────────────┐
│                        Worker Process                             │
├──────────────────────────────────────────────────────────────────┤
│  4. IHttpCoordinator synchronizes gRPC + HTTP                     │
│     [Worker's FunctionsHttpProxyingMiddleware]                    │
│     ┌────────────────────────────────────────┐                   │
│     │ Wait:    For HTTP request via invocation ID                │
│     │ Match:   gRPC FunctionContext with HttpContext             │
│     │ Active:  null                           │                   │
│     └────────────────────────────────────────┘                   │
│                     │                                              │
│                     ↓                                              │
│  5. FunctionExecutionMiddleware.Invoke                            │
│     [FunctionExecutionMiddlewareInvokeIntegration.OnMethodBegin]  │
│     ┌────────────────────────────────────────┐                   │
│     │ Extract: HTTP headers (parent_id=H2)   │                   │
│     │ Create:  azure_functions.invoke (W1)   │                   │
│     │          parent=H2, trace_id=TID        │                   │
│     │ Active:  azure_functions.invoke (W1)   │                   │
│     └────────────────────────────────────────┘                   │
│                     │                                              │
│                     │ Active: Function span (W1)                 │
│                     ↓                                              │
│  6. User function executes                                        │
│     - Spans created here are children of W1                       │
│                     ↓                                              │
│  7. Close function span (W1)                                      │
│     [FunctionExecutionMiddlewareInvokeIntegration.OnMethodEnd]    │
│     Send HTTP response                                            │
└──────────────────────────────────────────────────────────────────┘
                      │
                      │ HTTP response
                      ↓
┌──────────────────────────────────────────────────────────────────┐
│                         Host Process                              │
│  8. Close http.request span (H2)                                  │
│     [Standard HTTP client instrumentation]                        │
│  9. Close azure_functions.invoke span (H1)                        │
│     [FunctionInvocationMiddlewareInvokeIntegration.OnMethodEnd]   │
└──────────────────────────────────────────────────────────────────┘

Result: Distributed trace with spans from two processes
  Host:   azure_functions.invoke (H1)
          └─ http.request (H2)
  Worker:    └─ azure_functions.invoke (W1, parent=H2)
```

---

### Isolated HTTP Trigger (Standard Mode)

```
┌──────────────────────────────────────────────────────────────────┐
│                         Host Process                              │
├──────────────────────────────────────────────────────────────────┤
│  1. FunctionInvocationMiddleware.Invoke                           │
│     [FunctionInvocationMiddlewareInvokeIntegration.OnMethodBegin] │
│     ┌────────────────────────────────────────┐                   │
│     │ Extract: HTTP headers (W3C/Datadog)    │                   │
│     │ Create:  azure_functions.invoke (H1)   │                   │
│     │ Active:  azure_functions.invoke (H1)   │                   │
│     └────────────────────────────────────────┘                   │
│                     │                                              │
│                     ↓                                              │
│  2. GrpcMessageConversionExtensions.ToRpcHttp                     │
│     [GrpcMessageConversionExtensionsToRpcHttpIntegration]         │
│     ┌────────────────────────────────────────┐                   │
│     │ Read:    Active scope (H1)             │                   │
│     │ Inject:  trace_id, parent_id=H1        │                   │
│     │          into RpcHttp.Headers           │                   │
│     │ Active:  azure_functions.invoke (H1)   │                   │
│     └────────────────────────────────────────┘                   │
│                     │                                              │
│                     ↓                                              │
│  3. FunctionExecutor.TryExecuteAsync                              │
│     [AzureFunctionsExecutorTryExecuteAsyncIntegration]            │
│     ┌────────────────────────────────────────┐                   │
│     │ Update:  Root span (H1) with metadata  │                   │
│     │ Active:  azure_functions.invoke (H1)   │                   │
│     └────────────────────────────────────────┘                   │
│                     │                                              │
│                     ↓                                              │
│  4. Send gRPC InvocationRequest to worker                         │
│     ┌────────────────────────────────────────┐                   │
│     │ RpcHttp.Headers:                       │                   │
│     │   x-datadog-trace-id: TID              │                   │
│     │   x-datadog-parent-id: H1              │                   │
│     └────────────────────────────────────────┘                   │
└─────────────────────┼────────────────────────────────────────────┘
                      │
                      │ gRPC with trace context
                      ↓
┌──────────────────────────────────────────────────────────────────┐
│                        Worker Process                             │
├──────────────────────────────────────────────────────────────────┤
│  5. FunctionExecutionMiddleware.Invoke                            │
│     [FunctionExecutionMiddlewareInvokeIntegration.OnMethodBegin]  │
│     ┌────────────────────────────────────────┐                   │
│     │ Extract: RpcHttp.Headers               │                   │
│     │          parent_id=H1, trace_id=TID     │                   │
│     │ Create:  azure_functions.invoke (W1)   │                   │
│     │          parent=H1, trace_id=TID        │                   │
│     │ Active:  azure_functions.invoke (W1)   │                   │
│     └────────────────────────────────────────┘                   │
│                     │                                              │
│                     │ Active: Function span (W1)                 │
│                     ↓                                              │
│  6. User function executes                                        │
│     - Spans created here are children of W1                       │
│                     ↓                                              │
│  7. Close function span (W1)                                      │
│     [FunctionExecutionMiddlewareInvokeIntegration.OnMethodEnd]    │
│     Send gRPC InvocationResponse                                  │
└──────────────────────────────────────────────────────────────────┘
                      │
                      │ gRPC response
                      ↓
┌──────────────────────────────────────────────────────────────────┐
│                         Host Process                              │
│  8. Close azure_functions.invoke span (H1)                        │
│     [FunctionInvocationMiddlewareInvokeIntegration.OnMethodEnd]   │
└──────────────────────────────────────────────────────────────────┘

Result: Distributed trace with spans from two processes
  Host:   azure_functions.invoke (H1)
  Worker: └─ azure_functions.invoke (W1, parent=H1)
```

---

### In-Process HTTP Trigger

```
┌──────────────────────────────────────────────────────────────────┐
│                         Host Process                              │
├──────────────────────────────────────────────────────────────────┤
│  1. FunctionInvocationMiddleware.Invoke                           │
│     [FunctionInvocationMiddlewareInvokeIntegration.OnMethodBegin] │
│     ┌────────────────────────────────────────┐                   │
│     │ Extract: HTTP headers (W3C/Datadog)    │                   │
│     │ Create:  azure_functions.invoke (H1)   │                   │
│     │ Store:   Scope in CallTargetState      │                   │
│     │ Active:  azure_functions.invoke (H1)   │                   │
│     └────────────────────────────────────────┘                   │
│                     │                                              │
│                     │ Active: HTTP span (H1)                     │
│                     ↓                                              │
│  2. FunctionExecutor.TryExecuteAsync                              │
│     [AzureFunctionsExecutorTryExecuteAsyncIntegration]            │
│     ┌────────────────────────────────────────┐                   │
│     │ Read:    IFunctionInstance metadata    │                   │
│     │ Create:  azure_functions.invoke (F1)   │                   │
│     │          parent=H1 (nested span)        │                   │
│     │ Update:  Root span (H1) tags           │                   │
│     │ Active:  azure_functions.invoke (F1)   │                   │
│     └────────────────────────────────────────┘                   │
│                     │                                              │
│                     │ Active: Function span (F1)                 │
│                     ↓                                              │
│  3. User function executes                                        │
│     - Spans created here are children of F1                       │
│                     ↓                                              │
│  4. Close function span (F1)                                      │
│     [AzureFunctionsExecutorTryExecuteAsyncIntegration.OnMethodEnd]│
│     Active: HTTP span (H1)                                        │
│                     ↓                                              │
│  5. Close HTTP span (H1)                                          │
│     [FunctionInvocationMiddlewareInvokeIntegration.OnMethodEnd]   │
│     Active: null                                                  │
└──────────────────────────────────────────────────────────────────┘

Result: Single trace with nested spans
  azure_functions.invoke (H1, HTTP trigger)
  └─ azure_functions.invoke (F1, function execution)
     └─ ... (user-created spans)
```

---

## Frequently Asked Questions

### Is AspNetCoreDiagnosticObserver used at all for HTTP triggers?

**Short answer**: **No, never**. It is completely disabled in all Azure Functions scenarios.

**Detailed answer**:

`AspNetCoreDiagnosticObserver` is **explicitly disabled** in Azure Functions environments (both host and worker processes) by checking for the presence of `FUNCTIONS_EXTENSION_VERSION` and `FUNCTIONS_WORKER_RUNTIME` environment variables (see `Instrumentation.cs:473-489`).

This applies to:
- ❌ **In-Process Functions**: Disabled
- ❌ **Isolated Functions (Standard Mode)**: Disabled in both host and worker
- ❌ **Isolated Functions (ASP.NET Core Integration)**: Disabled in both host and worker

**Why disabled?**

From the code comments in `Instrumentation.cs`:
> "Not adding the `AspNetCoreDiagnosticObserver` is particularly important for in-process Azure Functions. The AspNetCoreDiagnosticObserver will be loaded in a separate Assembly Load Context, breaking the connection of AsyncLocal. This is because user code is loaded within the functions host in a separate context. **Even in isolated functions, we don't want the AspNetCore spans to be created.**"

Instead, we rely entirely on **CallTarget instrumentation**:
- `FunctionInvocationMiddleware.Invoke` (host) for HTTP request spans
- `FunctionExecutionMiddleware.Invoke` (worker) for function execution spans

### In isolated function HTTP triggers using ASP.NET Core integration, is gRPC used at all?

**Short answer**: Yes, gRPC is still used in addition to HTTP proxying.

**Detailed answer**:

Both mechanisms work in parallel:
1. **gRPC InvocationRequest**: Sent to the worker to provide function invocation context (function name, invocation ID, metadata, etc.)
2. **HTTP Proxying**: The original HTTP request is forwarded directly to the worker to provide the actual HTTP request data

This is visible in the Azure Functions host source code at [GrpcWorkerChannel.cs:900-903](https://github.com/Azure/azure-functions-host/blob/dev/src/WebJobs.Script.Grpc/Channel/GrpcWorkerChannel.cs#L900-L903):

```csharp
// If the worker supports HTTP proxying, ensure this request is forwarded prior
// to sending the invocation request to the worker
if (IsHttpProxyingWorker && context.FunctionMetadata.IsHttpTriggerFunction())
{
    _httpProxyService.StartForwarding(context, _httpProxyEndpoint);
}

await SendStreamingMessageAsync(new StreamingMessage
{
    InvocationRequest = invocationRequest
});
```

**Why both?**
- The gRPC message provides the function invocation context needed by `FunctionExecutionMiddleware`
- The HTTP request provides the actual HTTP data that the function needs to process
- The `ToRpcHttp()` method returns a nearly empty `TypedData` when proxying is enabled, but the gRPC message is still sent

This is different from the standard isolated mode where ALL data (including HTTP request data) flows over gRPC.

---

## Summary

### When Each Integration Fires

| Integration | Process | Timing | Creates Span? | Purpose |
|-------------|---------|--------|---------------|---------|
| `FunctionInvocationMiddleware.Invoke` | Host | When HTTP request received | **Yes** (HTTP span) | Extract client context, create outer span |
| `GrpcMessageConversionExtensions.ToRpcHttp` | Host | When converting HTTP → gRPC | No | Inject host span context into gRPC message |
| `FunctionExecutor.TryExecuteAsync` | Host | Before function execution | **Sometimes** (in-process only) | Add function metadata to span(s) |
| `FunctionExecutionMiddleware.Invoke` | Worker | When worker executes function | **Yes** (function span) | Extract gRPC context, create function span |
| `AspNetCoreDiagnosticObserver` | Worker | When HTTP request received (ASP.NET Core integration) | **Yes** (ASP.NET Core span) | Extract HTTP context, create ASP.NET Core span |

---

### Context Flow Patterns

**In-Process**:
- Context flows **in-memory** within a single process
- `Tracer.InternalActiveScope` provides access to the active span
- Nested spans automatically inherit parent context

**Isolated (Standard Mode)**:
- Context flows **via gRPC message headers** (`RpcHttp.Headers`)
- Host injects context → Worker extracts context
- Spans sent to Agent independently from each process

**Isolated (ASP.NET Core Integration)**:
- Context flows **via HTTP headers** in the proxied request
- Host's HTTP client instrumentation injects context → Worker's ASP.NET Core instrumentation extracts context
- Additional layer: Worker's function span is nested under ASP.NET Core span

---

## Related Issues

- **[APMSVLS-58: AsyncLocal Context Flow Issue](investigations/APMSVLS-58-AsyncLocal-Context-Flow.md)** - Known issue where host and worker spans may not connect correctly in ASP.NET Core integration mode, resulting in separate traces instead of a unified distributed trace.

---

**Document Version**: 1.0
**Last Updated**: 2025-01-21
**Author**: Lucas Pimentel (@lucaspimentel)
