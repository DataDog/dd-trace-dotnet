# Azure Functions Architecture Deep Dive

This document provides detailed architectural information about Azure Functions Host and .NET Worker, focusing on aspects relevant to dd-trace-dotnet instrumentation and distributed tracing.

**Related Documentation:**
- [Azure Functions Integration Guide](../AzureFunctions.md) - Setup, testing, and instrumentation specifics for dd-trace-dotnet
- [AGENTS.md](../../../AGENTS.md) - Repository structure and development guidelines

**External Resources:**
- [Azure Functions Host Repository](https://github.com/Azure/azure-functions-host)
- [Azure Functions .NET Worker Repository](https://github.com/Azure/azure-functions-dotnet-worker)

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Azure Functions Host](#azure-functions-host)
3. [Azure Functions .NET Worker](#azure-functions-net-worker)
4. [gRPC Communication Protocol](#grpc-communication-protocol)
5. [Distributed Tracing Integration](#distributed-tracing-integration)
6. [Environment Variables and Configuration](#environment-variables-and-configuration)
7. [Instrumentation Hook Points](#instrumentation-hook-points)

---

## Architecture Overview

Azure Functions uses a **host-worker architecture** where:
- **Host**: Manages triggers, bindings, scaling, and routes invocations
- **Worker**: Executes user function code in an isolated process
- **Communication**: Bidirectional gRPC streaming between host and worker

### Execution Models

#### In-Process (.NET)
- Function code runs in the same process as the host
- Direct assembly loading and execution
- Tightly coupled to host runtime version
- Limited to .NET version supported by host

#### Isolated Worker (.NET Isolated)
- Function code runs in a separate process
- gRPC communication between processes
- Independent .NET version from host
- Supports middleware and full DI control

**dd-trace-dotnet supports both models** but uses different instrumentation strategies.

---

## Azure Functions Host

**Repository**: [azure-functions-host](https://github.com/Azure/azure-functions-host)
**Solution**: `WebJobs.Script.sln`

### Core Components

#### 1. ScriptHost
**Location**: `src/WebJobs.Script/Host/ScriptHost.cs`

The ScriptHost is the central orchestrator that manages the function execution lifecycle.

**Key Responsibilities**:
- Function metadata management and validation
- Worker channel initialization and lifecycle
- Extension and binding provider coordination
- Function descriptor generation
- Primary/secondary host state management

**Initialization Flow**:
1. **PreInitialize()** (lines 429-473):
   - Validates `FUNCTIONS_EXTENSION_VERSION`
   - Validates `FUNCTIONS_WORKER_RUNTIME`
   - Logs Application Insights and OpenTelemetry status
   - Initializes file system directories

2. **InitializeAsync()** (lines 277-329):
   - Generates function metadata
   - Resolves worker runtime
   - Creates function descriptors
   - Initializes function dispatcher
   - Generates IL function wrappers

3. **StartAsyncCore()** (lines 256-271):
   - Logs initialization metrics
   - Starts the JobHost
   - Logs function errors

#### 2. RpcFunctionInvocationDispatcher
**Location**: `src/WebJobs.Script/Workers/Rpc/FunctionRegistration/RpcFunctionInvocationDispatcher.cs`

Manages RPC-based worker processes and routes invocations.

**Key Methods**:
- **InitializeAsync()** (lines 255-367):
  - Resolves worker runtime from `FUNCTIONS_WORKER_RUNTIME` or metadata
  - Validates worker configuration
  - Starts worker processes
  - Initializes worker channels

- **InvokeAsync()** (lines 412-434):
  - Gets initialized worker channels for function's language
  - Uses load balancer to select channel
  - Posts invocation to function's input buffer

- **DisposeAndRestartWorkerChannel()** (lines 526-578):
  - Shuts down failed worker
  - Restarts if needed based on error threshold

#### 3. GrpcWorkerChannel
**Location**: `src/WebJobs.Script.Grpc/Channel/GrpcWorkerChannel.cs`

Represents a single gRPC communication channel to a worker process.

**Lifecycle**:
1. **Constructor** (lines 98-160): Sets up channels, metrics, event subscriptions
2. **StartWorkerProcessAsync()** (lines 373-383): Starts process and waits for init
3. **SendWorkerInitRequest()** (lines 422-453): Sends capabilities to worker
4. **WorkerInitResponse()** (lines 493-521): Receives worker capabilities
5. **SendFunctionLoadRequests()** (lines 606-640): Loads functions into worker
6. **SendInvocationRequest()** (lines 852-920): Sends invocation with trace context
7. **InvokeResponse()** (lines 1100-1152): Processes invocation result

### Worker Process Management

#### Process Configuration
**Location**: `src/WebJobs.Script/Workers/ProcessManagement/WorkerProcessCountOptions.cs`

Default values:
- `ProcessCount`: 1
- `MaxProcessCount`: 10
- `ProcessStartupInterval`: 10 seconds
- `ProcessStartupTimeout`: 60 seconds
- `InitializationTimeout`: 10 seconds
- `EnvironmentReloadTimeout`: 30 seconds

#### Process Pool Management
- Dynamic concurrency: Max 1 worker
- Regular mode: Up to `MaxProcessCount` workers
- Error threshold: 3 × MaxProcessCount
- Restart interval: 5 minutes between restarts

### Extension System

#### Extension Loading
Extensions are loaded through:
1. **IScriptBindingProvider**: Binding providers registered via DI
2. **Extension Bundles**: Versioned extension packages via `IExtensionBundleManager`
3. **Function Descriptor Providers**:
   - `DotNetFunctionDescriptorProvider`: In-proc .NET functions
   - `RpcFunctionDescriptorProvider`: Out-of-proc language workers
   - `HttpFunctionDescriptorProvider`: HTTP workers
   - `CodelessFunctionDescriptorProvider`: Pre-compiled assemblies

### gRPC Server

#### FunctionRpcService
**Location**: `src/WebJobs.Script.Grpc/Server/FunctionRpcService.cs`

Implements the gRPC service for bidirectional streaming:

**EventStream()** (lines 33-101):
- Waits for `StartStream` message with worker ID
- Retrieves inbound/outbound channels from `ScriptEventManager`
- Spawns `PushFromOutboundToGrpc()` background task
- Loops reading from gRPC and writing to inbound channel

**Architecture**:
- Uses `System.Threading.Channels` for internal message queuing
- `InboundGrpcEvent`: Messages from worker to host
- `OutboundGrpcEvent`: Messages from host to worker

---

## Azure Functions .NET Worker

**Repository**: [azure-functions-dotnet-worker](https://github.com/Azure/azure-functions-dotnet-worker)
**Main Solution**: `DotNetWorker.sln`

### Core Components

#### 1. FunctionsApplication
**Location**: `src/DotNetWorker.Core/FunctionsApplication.cs`

Core orchestrator that manages function execution.

**Key Responsibilities**:
- Maintains function definition registry (`_functionMap`)
- Creates `FunctionContext` for each invocation
- Orchestrates middleware pipeline execution
- Manages distributed tracing via `Activity`

**Key Methods**:
- **CreateContext()** (line 43): Creates `FunctionContext` from invocation features
- **LoadFunction()** (line 53): Registers function definitions
- **InvokeFunctionAsync()** (lines 68-109): Executes middleware pipeline with tracing
  - Parses W3C TraceContext from `context.TraceContext`
  - Creates internal `Activity` if needed
  - Starts OpenTelemetry Activity
  - Executes middleware pipeline
  - Handles exceptions and sets Activity status

#### 2. GrpcWorker
**Location**: `src/DotNetWorker.Grpc/GrpcWorker.cs`

Implements bidirectional gRPC streaming communication with host.

**Message Types Handled**:
- `InvocationRequest` → Delegates to `InvocationHandler`
- `FunctionLoadRequest` → Loads function definitions
- `WorkerInitRequest` → Reports capabilities and metadata
- `FunctionsMetadataRequest` → Provides function indexing metadata
- `InvocationCancel` → Cancellation token management
- `WorkerTerminate` → Graceful shutdown

**StartAsync()** (lines 54-58):
1. Initiates gRPC `EventStream` call
2. Sends `StartStream` message with worker ID
3. Starts writer task for responses
4. Starts reader task for requests

#### 3. InvocationHandler
**Location**: `src/DotNetWorker.Grpc/Handlers/InvocationHandler.cs`

Processes invocation requests and manages invocation lifecycle.

**InvokeAsync() Flow** (lines 54-142):
1. Create `CancellationTokenSource` and track invocation
2. Create `GrpcFunctionInvocation` from request
3. Build `IInvocationFeatures` collection
4. Create `FunctionContext` via `FunctionsApplication`
5. Add `IFunctionBindingsFeature` with gRPC binding data
6. Invoke function via `_application.InvokeFunctionAsync(context)`
7. Serialize output bindings and return value
8. Clean up resources and dispose context

### Middleware Model

The worker uses an ASP.NET Core-style middleware pattern.

#### Middleware Interface
**Location**: `src/DotNetWorker.Core/Pipeline/IFunctionsWorkerMiddleware.cs`

```csharp
public interface IFunctionsWorkerMiddleware
{
    Task Invoke(FunctionContext context, FunctionExecutionDelegate next);
}
```

#### Pipeline Builder
**Location**: `src/DotNetWorker.Core/Pipeline/DefaultInvocationPipelineBuilder.cs`

Middleware is built using reverse aggregation (lines 24-33):
```csharp
FunctionExecutionDelegate pipeline = context => Task.CompletedTask;
pipeline = _middlewareCollection.Reverse().Aggregate(pipeline, (p, d) => d(p));
```

#### Default Middleware Order
**Location**: `src/DotNetWorker.Core/Hosting/WorkerMiddlewareWorkerApplicationBuilderExtensions.cs`

1. **Custom Middleware** (registered first)
2. **OutputBindingsMiddleware** (line 35): Binds output data from function result
3. **FunctionExecutionMiddleware** (line 36): Delegates to `IFunctionExecutor.ExecuteAsync()`

#### Middleware Registration
**Location**: `src/DotNetWorker.Core/Hosting/WorkerMiddlewareWorkerApplicationBuilderExtensions.cs`

```csharp
// Register typed middleware
builder.UseMiddleware<MyMiddleware>();

// Register inline middleware
builder.UseMiddleware(async (FunctionContext context, FunctionExecutionDelegate next) => {
    // Pre-execution logic
    await next(context);
    // Post-execution logic
});

// Conditional middleware
builder.UseWhen<MyMiddleware>(context =>
    context.FunctionDefinition.Name == "MyFunction");
```

### Function Execution Pipeline

#### FunctionExecutor
**Location**: `src/DotNetWorker.Core/Invocation/DefaultFunctionExecutor.cs`

**ExecuteAsync Flow** (lines 25-50):
1. Get or create `IFunctionInvoker` from cache
2. Create function instance via `IFunctionActivator`
3. Bind input parameters via `IFunctionInputBindingFeature`
4. Invoke the method and store result

#### FunctionActivator
**Location**: `src/DotNetWorker.Core/Invocation/DefaultFunctionActivator.cs`

Uses `ActivatorUtilities.CreateInstance()` for constructor injection:
```csharp
return ActivatorUtilities.CreateInstance(
    context.InstanceServices,
    instanceType,
    Array.Empty<object>());
```

### FunctionContext

**Location**: `src/DotNetWorker.Core/Context/FunctionContext.cs`

Abstract class providing access to invocation data.

**Key Properties**:
- `InvocationId`: Unique identifier for invocation
- `FunctionId`: Stable identifier for function
- `TraceContext`: Distributed tracing context (TraceParent, TraceState)
- `BindingContext`: Access to binding data
- `RetryContext`: Retry information
- `InstanceServices`: Scoped service provider for DI
- `FunctionDefinition`: Function metadata
- `Items`: Request-scoped key/value storage
- `Features`: Extensible feature collection
- `CancellationToken`: Invocation cancellation token

#### Accessing Bindings
**Extension Method**: `context.GetBindings()`

Returns `IFunctionBindingsFeature`:
- `TriggerMetadata`: Read-only dictionary with trigger metadata
- `InputData`: Read-only dictionary with input binding data
- `OutputBindingData`: Mutable dictionary for output bindings
- `InvocationResult`: Function return value

### Startup and Initialization

#### StartupHook
**Location**: `src/DotNetWorker.Core/StartupHook.cs`

Uses .NET's startup hook feature for early initialization.

**Actions** (lines 26-72):
1. **Self-Removal**: Removes itself from `DOTNET_STARTUP_HOOKS` environment variable
2. **Debugger Wait**: If `FUNCTIONS_ENABLE_DEBUGGER_WAIT=true`, waits for debugger
3. **JSON Output**: If `FUNCTIONS_ENABLE_JSON_OUTPUT=true`, emits startup log

#### Worker Initialization Sequence
1. `StartupHook.Initialize()` - Early initialization (before Main)
2. `Program.Main()` - User code creates `IHostBuilder`
3. `ConfigureFunctionsWorkerDefaults()` - Registers worker services
4. `AddFunctionsWorkerCore()` - Core service registration
5. `AddGrpc()` - gRPC services registration
6. `GrpcWorker.StartAsync()` - Establishes gRPC connection

### Service Registration

#### Core Services
**Location**: `src/DotNetWorker.Core/Hosting/ServiceCollectionExtensions.cs` (lines 36-118)

**Request Handling**:
- `IFunctionsApplication` → `FunctionsApplication`

**Function Execution**:
- `IMethodInfoLocator` → `DefaultMethodInfoLocator`
- `IFunctionInvokerFactory` → `DefaultFunctionInvokerFactory`
- `IMethodInvokerFactory` → `DefaultMethodInvokerFactory`
- `IFunctionActivator` → `DefaultFunctionActivator`
- `IFunctionExecutor` → `DefaultFunctionExecutor`

**Context Management**:
- `IFunctionContextFactory` → `DefaultFunctionContextFactory`
- `IInvocationFeaturesFactory` → `DefaultInvocationFeaturesFactory`

**Diagnostics**:
- `FunctionActivitySourceFactory`
- `ILoggerProvider` → `WorkerLoggerProvider`

#### gRPC Services
**Location**: `src/DotNetWorker.Grpc/GrpcServiceCollectionExtensions.cs` (lines 38-76)

- `GrpcHostChannel`: Unbounded channel for output messages
- `GrpcFunctionsHostLogWriter`: Logging integration
- `IWorker` → `GrpcWorker`
- `IInvocationHandler` → `InvocationHandler`
- `IWorkerClientFactory` → `GrpcWorkerClientFactory`

---

## gRPC Communication Protocol

**Protocol Definition**: `azure-functions-language-worker-protobuf/src/proto/FunctionRpc.proto`

### Service Definition

```protobuf
service FunctionRpc {
  rpc EventStream (stream StreamingMessage) returns (stream StreamingMessage) {}
}
```

Bidirectional streaming with multiplexed message types.

### Key Message Types

#### StreamingMessage
**Lines 21-97**

Wrapper for all bidirectional messages with `oneof content`:
- `StartStream`: Worker initiates connection
- `WorkerInitRequest/Response`: Initialization handshake
- `WorkerStatusRequest/Response`: Health checks
- `FunctionLoadRequest/Response`: Function loading
- `InvocationRequest/Response`: Function execution
- `InvocationCancel`: Cancel running invocation
- `RpcLog`: Worker logs
- `FunctionEnvironmentReloadRequest/Response`: Specialization
- `FunctionsMetadataRequest/FunctionMetadataResponse`: Worker indexing

#### InvocationRequest
**Lines 379-397**

```protobuf
message InvocationRequest {
  string invocation_id = 1;
  string function_id = 2;
  repeated ParameterBinding input_data = 3;
  map<string, TypedData> trigger_metadata = 4;
  RpcTraceContext trace_context = 6;
  RetryContext retry_context = 7;
  // ...
}
```

#### InvocationResponse
**Lines 433-445**

```protobuf
message InvocationResponse {
  string invocation_id = 1;
  repeated ParameterBinding output_data = 2;
  TypedData return_value = 4;
  StatusResult result = 3;
  // ...
}
```

#### RpcTraceContext
**Lines 400-409**

```protobuf
message RpcTraceContext {
  string trace_parent = 1;    // W3C traceparent header
  string trace_state = 2;     // W3C tracestate header
  map<string, string> attributes = 3;  // Additional tags
}
```

#### TypedData
**Lines 457-473**

Polymorphic data container supporting:
- `string`, `json`, `bytes`, `stream`
- `http`: `RpcHttp` with headers, body, cookies
- Numeric: `int`, `double`
- Collections: `bytes`, `string`, `double`, `sint64`
- `model_binding_data`: SDK-type bindings

### Communication Flow

#### Initialization
1. **Host starts worker process** with command-line arguments:
   - `--functions-uri`: gRPC endpoint
   - `--functions-worker-id`: Worker identifier
   - `--functions-request-id`: Request identifier

2. **Worker connects** to gRPC endpoint and sends `StartStream` message

3. **Host sends `WorkerInitRequest`** with host capabilities:
   - Shared memory support
   - Function data cache
   - V2 compatibility flags

4. **Worker responds with `WorkerInitResponse`** with worker capabilities:
   - Cancellation support
   - Worker termination handling
   - Raw HTTP body support

5. **Host sends `FunctionLoadRequest`** for each function

6. **Worker responds with `FunctionLoadResponse`** for each function

#### Invocation
1. **Host sends `InvocationRequest`**:
   - Function ID
   - Input bindings
   - Trigger metadata
   - **Trace context** (W3C TraceContext)

2. **Worker processes invocation**:
   - Parses trace context
   - Creates Activity
   - Executes middleware pipeline
   - Invokes function

3. **Worker sends `InvocationResponse`**:
   - Output bindings
   - Return value
   - Success/failure status

#### Cancellation
1. **Host sends `InvocationCancel`** with invocation ID
2. **Worker cancels CancellationToken** for invocation
3. **Worker sends `InvocationResponse`** with `Cancelled` status

---

## Distributed Tracing Integration

### W3C Trace Context Support

Both host and worker support W3C Trace Context propagation.

#### Host Side

**Location**: `src/WebJobs.Script.Grpc/Channel/GrpcWorkerChannel.cs`

**SendInvocationRequest()** (lines 852-920):
- Adds distributed tracing context to `InvocationRequest`
- Sets `trace_context.trace_parent` from `Activity.Current?.Id`
- Sets `trace_context.trace_state` from `Activity.Current?.TraceStateString`

**AddAdditionalTraceContext()** (lines 1684-1728):
- Adds tags to `Activity.Current` from gRPC `RpcTraceContext.Attributes`
- Called when receiving `InvocationResponse`

#### Worker Side

**Location**: `src/DotNetWorker.Core/FunctionsApplication.cs`

**InvokeFunctionAsync()** (lines 68-109):
1. **Parse W3C TraceContext** (lines 78-86):
   ```csharp
   if (ActivityContext.TryParse(context.TraceContext.TraceParent,
                                 context.TraceContext.TraceState,
                                 out var activityContext))
   {
       var activity = new Activity("InboundRequest");
       activity.SetParentId(activityContext.TraceId, activityContext.SpanId, activityContext.TraceFlags);
       activity.TraceStateString = activityContext.TraceState;
       activity.Start();
   }
   ```

2. **Start OpenTelemetry Activity** (line 92):
   ```csharp
   using var activity = _functionActivitySource.StartInvoke(context);
   ```

3. **Execute middleware pipeline** (line 96):
   ```csharp
   await _functionExecutionDelegate(context);
   ```

4. **Set Activity status on exception** (lines 98-105)

### Activity Integration

#### FunctionActivitySourceFactory
**Location**: `src/DotNetWorker.Core/Diagnostics/FunctionActivitySourceFactory.cs`

**ActivitySource**: `"Microsoft.Azure.Functions.Worker"` version `"1.0.0.0"`

**StartInvoke()** (lines 24-37):
- Creates Activity with `ActivityKind.Server`
- Adds tags: `az.schema_url`, `faas.execution` (invocation ID)
- Uses OpenTelemetry 1.17.0 schema

### Application Insights Integration

The host logs Application Insights status during initialization.

**Location**: `src/WebJobs.Script/Host/ScriptHost.cs` (PreInitialize)

Checks for:
- `APPLICATIONINSIGHTS_CONNECTION_STRING`
- `APPINSIGHTS_INSTRUMENTATIONKEY`
- `APPLICATIONINSIGHTS_ENABLE_AGENT`

### OpenTelemetry Support

The host logs OpenTelemetry status during initialization.

**Location**: `src/WebJobs.Script/Host/ScriptHost.cs` (PreInitialize)

Checks for:
- `OTEL_EXPORTER_OTLP_ENDPOINT`

---

## Environment Variables and Configuration

### Host Environment Variables

**Location**: `src/WebJobs.Script/Environment/EnvironmentSettingNames.cs`

#### Core Runtime Settings
- `FUNCTIONS_EXTENSION_VERSION`: Functions runtime version (e.g., "~4")
- `FUNCTIONS_WORKER_RUNTIME`: Language runtime (node, python, dotnet-isolated)
- `FUNCTIONS_WORKER_RUNTIME_VERSION`: Specific runtime version
- `AzureWebJobsScriptRoot`: Function app root path

#### Worker Process Configuration
- `FUNCTIONS_WORKER_PROCESS_COUNT`: Number of worker processes
- `FUNCTIONS_WORKER_SHARED_MEMORY_DATA_TRANSFER_ENABLED`: Enable shared memory
- `FUNCTIONS_UNIX_SHARED_MEMORY_DIRECTORIES`: Custom shared memory directories
- `FUNCTIONS_WORKER_DYNAMIC_CONCURRENCY_ENABLED`: Enable dynamic concurrency

#### Placeholder/Specialization
- `WEBSITE_PLACEHOLDER_MODE`: Running in placeholder mode
- `WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED`: Use .NET isolated placeholder
- `INITIALIZED_FROM_PLACEHOLDER`: Specialized from placeholder
- `WEBSITE_CONTAINER_READY`: Container ready for specialization

#### Azure App Service
- `WEBSITE_SITE_NAME`: Site name
- `WEBSITE_HOSTNAME`: Hostname
- `WEBSITE_INSTANCE_ID`: Instance ID
- `WEBSITE_SKU`: SKU (Dynamic, ElasticPremium, etc.)
- `WEBSITE_OWNER_NAME`: Subscription/resource group info

#### Telemetry
- `APPLICATIONINSIGHTS_CONNECTION_STRING`: App Insights connection string
- `APPINSIGHTS_INSTRUMENTATIONKEY`: Legacy instrumentation key
- `APPLICATIONINSIGHTS_ENABLE_AGENT`: Enable App Insights agent
- `OTEL_EXPORTER_OTLP_ENDPOINT`: OpenTelemetry endpoint

#### Storage
- `AzureWebJobsStorage`: Storage account connection string
- `WEBSITE_CONTENTAZUREFILECONNECTIONSTRING`: Azure Files connection
- `WEBSITE_CONTENTSHARE`: Content share name

#### Run from Package
- `WEBSITE_RUN_FROM_PACKAGE`: Run from package URL or path
- `WEBSITE_RUN_FROM_PACKAGE_BLOB_MI_RESOURCE_ID`: Managed identity for blob

### Worker Environment Variables

**Location**: `src/DotNetWorker.Core/StartupHook.cs` and `src/DotNetWorker.Grpc/GrpcServiceCollectionExtensions.cs`

#### Startup Hook Variables
- `DOTNET_STARTUP_HOOKS`: Automatically managed by worker
- `FUNCTIONS_ENABLE_DEBUGGER_WAIT`: Wait for debugger attach at startup
- `FUNCTIONS_ENABLE_JSON_OUTPUT`: Emit JSON startup logs

#### Worker Configuration Variables
- `AZURE_FUNCTIONS_*`: Host configuration (automatically loaded as config prefix)
- `Functions:Worker:HostEndpoint`: gRPC endpoint URI
- `Functions:Worker:WorkerId`: Worker identifier
- `Functions:Worker:RequestId`: Initial request identifier
- `Functions:Worker:GrpcMaxMessageLength`: Max message size

#### Application Directory Variables
- `FUNCTIONS_WORKER_DIRECTORY`: Worker directory (fallback)
- `FUNCTIONS_APPLICATION_DIRECTORY`: Application directory (preferred)

#### Native Host Integration
- `AZURE_FUNCTIONS_NATIVE_HOST`: AppContext data for native host integration

### Configuration Precedence

**Host Configuration Hierarchy** (highest to lowest):
1. Environment Variables
2. Application Settings (exposed as environment variables)
3. Host.json
4. Worker Config Files (worker.config.json)
5. Platform Defaults

**Worker Configuration Hierarchy** (highest to lowest):
1. Command Line Arguments (with switch mappings)
2. Environment Variables (all)
3. AZURE_FUNCTIONS_ Prefixed Environment Variables
4. Default Values (in code)

### Host.json Settings

Key host.json sections affecting workers:

#### languageWorkers
```json
{
  "languageWorkers": {
    "workersDirectory": "workers"
  }
}
```

#### Worker-specific sections
```json
{
  "node": {
    "maxWorkerProcessCount": 5
  },
  "dotnet-isolated": {
    "maxWorkerProcessCount": 3
  }
}
```

#### functionTimeout
Maximum execution time (affects worker behavior)

#### extensions
Extension-specific configuration:
- Worker concurrency settings
- Shared memory configuration
- Worker indexing flags

#### logging
Log levels affect `RpcLog` verbosity

---

## Instrumentation Hook Points

### For Host-Side Instrumentation

#### 1. Worker Process Startup
**Location**: `src/WebJobs.Script/Workers/ProcessManagement/WorkerProcess.cs`

**Hook Point**: Environment variables can be injected before `Process.Start()`

**Use Case**: Inject tracer configuration for worker process

#### 2. gRPC Message Interception
**Location**: `src/WebJobs.Script.Grpc/Server/FunctionRpcService.cs`

**Hook Point**: `EventStream()` sees all bidirectional messages

**Use Case**: Inspect or modify trace context in `InvocationRequest`/`InvocationResponse`

#### 3. Invocation Context
**Location**: `src/WebJobs.Script.Grpc/Channel/GrpcWorkerChannel.cs`

**Hook Points**:
- `SendInvocationRequest()` (line 892-893): Inject/extract trace context
- `InvokeResponse()` (line 1100-1152): Process response with trace context

**Use Case**: Add tags to `Activity.Current` from invocation metadata

#### 4. Distributed Tracing
**Location**: `src/WebJobs.Script.Grpc/Channel/GrpcWorkerChannel.cs`

**Hook Point**: `AddAdditionalTraceContext()` (lines 1684-1728)

**Use Case**: Add tags to Activity.Current from RpcTraceContext

### For Worker-Side Instrumentation

#### 1. Middleware (Primary Hook Point)
**Location**: User code via `IFunctionsWorkerApplicationBuilder`

**Hook Point**: Register middleware in `Program.cs`

**Use Case**: Pre/post function execution logic, distributed tracing, error handling

**Example**:
```csharp
public class DatadogTracingMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        // Extract trace context
        var traceParent = context.TraceContext.TraceParent;
        var traceState = context.TraceContext.TraceState;

        // Create span
        using (var scope = /* create Datadog scope */)
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                // Report exception to span
                throw;
            }
        }
    }
}
```

#### 2. IInvocationFeatures - Feature Injection
**Location**: `src/DotNetWorker.Core/Context/Features/IInvocationFeatures.cs`

**Hook Point**: Add custom features via `context.Features.Set<T>(instance)`

**Use Case**: Attach custom tracing data to invocation

#### 3. FunctionContext.Items - Request State
**Location**: `src/DotNetWorker.Core/Context/FunctionContext.cs`

**Hook Point**: Store custom data in `context.Items` dictionary

**Use Case**: Share tracing state across middleware

#### 4. Activity and ActivitySource Integration
**Location**: `src/DotNetWorker.Core/Diagnostics/FunctionActivitySourceFactory.cs`

**Hook Point**: Subscribe to `ActivitySource` with `ActivityListener`

**Use Case**: Intercept OpenTelemetry Activities for custom processing

**ActivitySource Name**: `"Microsoft.Azure.Functions.Worker"`

#### 5. IFunctionActivator - Instance Creation
**Location**: `src/DotNetWorker.Core/Invocation/IFunctionActivator.cs`

**Hook Point**: Replace default activator to inject custom logic

**Use Case**: Inject profiling or tracing logic during instance creation

#### 6. StartupHook - Early Initialization
**Location**: `src/DotNetWorker.Core/StartupHook.cs`

**Hook Point**: Add custom startup hook via `DOTNET_STARTUP_HOOKS`

**Use Case**: Initialize tracer before worker starts

### Recommended Instrumentation Strategy for dd-trace-dotnet

#### In-Process Model
1. Use CallTarget instrumentation for Azure Functions SDK types
2. Hook into JobHost invocation pipeline
3. Instrument binding-specific types (HTTP, Queue, etc.)

#### Isolated Worker Model
1. **Primary**: Use middleware for function invocation tracing
2. **Secondary**: Subscribe to ActivitySource for OpenTelemetry integration
3. **Advanced**: Use automatic instrumentation for gRPC client calls
4. **Context**: Extract W3C TraceContext from `FunctionContext.TraceContext`
5. **Propagation**: Inject trace headers into output bindings

### Key Integration Points for Datadog

#### Trace Context Extraction
**Worker Side**: `FunctionContext.TraceContext.TraceParent` and `TraceContext.TraceState`

Extract using W3C Trace Context format:
- TraceParent: `00-<trace-id>-<parent-id>-<trace-flags>`
- TraceState: Comma-separated list vendor state

#### Activity Integration
**Worker Side**: `FunctionsApplication.InvokeFunctionAsync()` already creates Activity

Datadog should:
1. Subscribe to ActivitySource `"Microsoft.Azure.Functions.Worker"`
2. Extract trace/span IDs from `Activity.Current`
3. Propagate Datadog-specific headers in `Activity.Baggage` or custom tags

#### Span Attributes
Recommended span attributes:
- `faas.trigger`: Trigger type (http, queue, timer, etc.)
- `faas.execution`: Invocation ID from `context.InvocationId`
- `faas.name`: Function name from `context.FunctionDefinition.Name`
- `cloud.provider`: "azure"
- `cloud.platform`: "azure_functions"
- `cloud.region`: From environment variables

#### Error Handling
Capture exceptions from:
1. **Middleware**: Catch exceptions in middleware
2. **Activity**: Set `Activity.Status` to `Error`
3. **Context**: Check `context.GetBindings().InvocationResult` for errors

---

## Additional Resources

### Source Code Locations

#### Host Repository
- **ScriptHost**: `src/WebJobs.Script/Host/ScriptHost.cs`
- **RpcFunctionInvocationDispatcher**: `src/WebJobs.Script/Workers/Rpc/FunctionRegistration/RpcFunctionInvocationDispatcher.cs`
- **GrpcWorkerChannel**: `src/WebJobs.Script.Grpc/Channel/GrpcWorkerChannel.cs`
- **FunctionRpcService**: `src/WebJobs.Script.Grpc/Server/FunctionRpcService.cs`
- **Environment Settings**: `src/WebJobs.Script/Environment/EnvironmentSettingNames.cs`
- **RpcWorkerConstants**: `src/WebJobs.Script/Workers/Rpc/RpcWorkerConstants.cs`

#### Worker Repository
- **FunctionsApplication**: `src/DotNetWorker.Core/FunctionsApplication.cs`
- **GrpcWorker**: `src/DotNetWorker.Grpc/GrpcWorker.cs`
- **InvocationHandler**: `src/DotNetWorker.Grpc/Handlers/InvocationHandler.cs`
- **FunctionContext**: `src/DotNetWorker.Core/Context/FunctionContext.cs`
- **Middleware Extensions**: `src/DotNetWorker.Core/Hosting/WorkerMiddlewareWorkerApplicationBuilderExtensions.cs`
- **FunctionActivitySourceFactory**: `src/DotNetWorker.Core/Diagnostics/FunctionActivitySourceFactory.cs`
- **StartupHook**: `src/DotNetWorker.Core/StartupHook.cs`

#### Protocol Definitions
- **FunctionRpc.proto**: `azure-functions-language-worker-protobuf/src/proto/FunctionRpc.proto` (in both repos)

### External Documentation

- [Azure Functions Overview](https://docs.microsoft.com/azure/azure-functions/)
- [Azure Functions Isolated Worker Guide](https://learn.microsoft.com/azure/azure-functions/dotnet-isolated-process-guide)
- [W3C Trace Context](https://www.w3.org/TR/trace-context/)
- [OpenTelemetry Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/)
- [gRPC Protocol](https://grpc.io/docs/)

### Related dd-trace-dotnet Documentation

- [AzureFunctions.md](../AzureFunctions.md) - Integration guide for dd-trace-dotnet
- [AutomaticInstrumentation.md](../AutomaticInstrumentation.md) - Creating integrations
- [DuckTyping.md](../DuckTyping.md) - Duck typing patterns for third-party types
- [AGENTS.md](../../../AGENTS.md) - Repository structure

---

**Document Version**: 1.0
**Last Updated**: 2025-01-09
**Maintainer**: Lucas Pimentel (@lucaspimentel)
