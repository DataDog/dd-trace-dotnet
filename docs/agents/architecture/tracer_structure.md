# Tracer Structure

## Core Managed Tracer Library

- `tracer/src/Datadog.Trace` — Core managed tracer library
  - `Activity` — System.Diagnostics.Activity bridge/helpers.
  - `Agent` — Agent transport, payloads, health, serialization.
  - `AppSec` — Application Security (WAF/RASP) components.
  - `Ci` — CI Visibility (test/session/span) logic.
  - `ClrProfiler` — Auto-instrumentation runtime (CallTarget, handlers, definitions).
  - `Configuration` — Settings, sources, environment parsing.
  - `ContinuousProfiler` — Hooks for CPU/wall profiler coordination.
  - `DataStreamsMonitoring` — DSM utilities and checkpoints.
  - `DatabaseMonitoring` — DBM helpers.
  - `Debugger` — Dynamic Instrumentation (live debugger) plumbing.
  - `DiagnosticListeners` — DiagnosticSource/Listener integrations.
  - `DogStatsd` — StatsD metrics client integration.
  - `DuckTyping` — Duck typing runtime and attributes.
  - `ExtensionMethods` — Internal extension helpers.
  - `Headers` — HTTP header constants/parsing.
  - `HttpOverStreams` — Socket/pipe HTTP transport to the agent.
  - `Iast` — Interactive App Security Testing.
  - `LibDatadog` — Native interop wrappers.
  - `Logging` — Internal logging abstractions.
  - `OTelMetrics` / `OpenTelemetry` — OTEL interop and exporters.
  - `PDBs` — Symbol/PDB helpers.
  - `PlatformHelpers` — OS/arch/runtime helpers.
  - `Processors` — Pipelines and span processors.
  - `Propagators` — Trace context inject/extract (Datadog, W3C, B3).
  - `RemoteConfigurationManagement` — RCM polling/apply.
  - `RuntimeMetrics` — Process/runtime metrics.
  - `Sampling` — Samplers and priorities.
  - `ServiceFabric` — Service Fabric integration helpers.
  - `Tagging` — Strongly-typed tag sets.
  - `Telemetry` — Product telemetry emission.
  - `Util` — Common utilities.
  - `Vendors` — Vendored third-party code.

## Other Tracer Modules

- Other tracer modules under `tracer/src`
  - `Datadog.Trace.ClrProfiler.Managed.Loader` — Managed bootstrapper loaded by the profiler.
  - `Datadog.Trace.Manual` — Manual instrumentation shims/APIs.
  - `Datadog.Trace.SourceGenerators` — Compile-time code generators.
  - `Datadog.Trace.OpenTracing` — OpenTracing bridge.
  - `Datadog.Trace.MSBuild` — MSBuild tasks/targets.
  - `Datadog.Trace.Tools.*` — CLI tools, analyzers, shared libs, and dd_dotnet.
  - `Datadog.Trace.Trimming` — Trimming/linker support.
  - `Datadog.AzureFunctions` — Azure Functions support.
  - `Datadog.FleetInstaller` — Fleet/installer utilities.
  - `Datadog.InstrumentedAssembly*` — Pre-instrumented assembly tooling/verification.
  - `Datadog.AutoInstrumentation.Generator` — Instrumentation metadata generators.
- `Datadog.Tracer.Native` — Native interop glue and packaging metadata.

## Detailed Tree Overview

```
tracer/src/Datadog.Trace
├─ Activity/               ─ Activity bridge/helpers
├─ Agent/                  ─ Agent transport and buffering
├─ AppSec/                 ─ Application security (WAF/RASP)
├─ AspNet/                 ─ ASP.NET helpers/back-compat
├─ Ci/                     ─ CI Visibility (tests, sessions)
├─ ClrProfiler/            ─ Auto-instrumentation runtime
├─ Configuration/          ─ Settings and sources
├─ ContinuousProfiler/     ─ Profiler coordination hooks
├─ DataStreamsMonitoring/  ─ DSM context/checkpoints
├─ DatabaseMonitoring/     ─ DB monitoring helpers
├─ Debugger/               ─ Dynamic Instrumentation
├─ DiagnosticListeners/    ─ DiagnosticSource integrations
├─ DogStatsd/              ─ StatsD integration
├─ DuckTyping/             ─ Duck typing runtime
├─ ExtensionMethods/       ─ Internal extensions
├─ FaultTolerant/          ─ Resilience helpers
├─ Generated/              ─ Generated sources
├─ Headers/                ─ HTTP header parsing/constants
├─ HttpOverStreams/        ─ Stream-based HTTP transport
├─ Iast/                   ─ Interactive AppSec Testing
├─ LibDatadog/             ─ Native interop
├─ Logging/                ─ Logging abstractions
├─ OTelMetrics/            ─ OTEL metrics bridge
├─ OpenTelemetry/          ─ OTEL trace interop
├─ PDBs/                   ─ Symbols helpers
├─ PlatformHelpers/        ─ OS/arch helpers
├─ Processors/             ─ Span processors
├─ Propagators/            ─ Context propagation
├─ RemoteConfigurationManagement/ ─ RCM
├─ RuntimeMetrics/         ─ Runtime metrics
├─ Sampling/               ─ Samplers/priorities
├─ ServiceFabric/          ─ Service Fabric helpers
├─ Tagging/                ─ Strong typed tags
├─ Telemetry/              ─ Product telemetry
├─ Util/                   ─ Utilities
└─ Vendors/                ─ Vendored deps
```

## Component Details

### ClrProfiler — Auto-instrumentation runtime
  - AutoInstrumentation — Integrations grouped by tech (AWS, AdoNet, AspNet/AspNetCore, Azure, Couchbase, Elasticsearch, GraphQL, Grpc, Http, IbmMq, Kafka, Logging, MongoDb, Msmq, OpenTelemetry, Process, Protobuf, RabbitMQ, Redis, Remoting, RestSharp, Testing, TraceAnnotations, Wcf).
  - CallTarget — Invoker, handlers, state structs, async continuations and helpers.
  - Helpers — IL/interop helpers; native definitions and memory helpers.
  - ServerlessInstrumentation — Serverless-specific hooks.

### Agent — Client and transports to the Datadog Agent
  - DiscoveryService — Detect agent endpoints/capabilities.
  - MessagePack — Trace payload encoding/formatting.
  - StreamFactories — HTTP/Unix/Windows stream implementations.
  - TraceSamplers — Client-side sampling strategies.
  - Transports — HTTP/pipes transport strategies and tuning.

### Configuration — Settings and sources
  - ConfigurationSources — Env vars, JSON, args, RCM providers.
  - Schema — Span attribute schema configuration.
  - Core — `TracerSettings`, `ExporterSettings`, `IntegrationSettings`, git metadata providers.

### Propagators — Context injection/extraction
  - Datadog, W3C (tracecontext/baggage), B3 (single/multiple header), factories/utilities.

### Telemetry — Product telemetry
  - Collectors — Feature/runtime collectors and samplers.
  - DTOs — Payload models and envelopes.
  - Metrics — Counters and series.
  - Transports — HTTP transport implementations and headers.

### Debugger — Dynamic Instrumentation (probes/snapshots)
  - Instrumentation, Snapshots, Upload, Caching, Configurations, Expressions, PInvoke, Symbols, ExceptionAutoInstrumentation, RateLimiting, Sink, SpanCodeOrigin.

### Iast — Interactive Application Security Testing
  - Aspects (sources/sinks), Dataflow (taint tracking), Propagation, SensitiveData, Settings, Telemetry, Analyzers, Helpers.

### DataStreamsMonitoring — DSM checkpoints and pathway context
  - Aggregation, Transport, Hashes, Utils; manager/writer and context propagator.

### Other Components
- RuntimeMetrics — Event/PerfCounters listeners and writers (AAS specifics included).
- Tagging — Strongly-typed tag classes per integration; TagPropagation/TagsList utilities.
- OpenTelemetry/OTelMetrics — OTEL bridges and exporters; builders and extension proxies.
- Processors — Span pipeline processors (e.g., trace/metrics enrichment).
- Sampling — Sampling strategies and priorities.
- Activity — Activity bridge + helpers for OpenTelemetry interop.
- DiagnosticListeners — DiagnosticSource/Listener-based integrations.
- DogStatsd — Direct StatsD metrics client support.
- DuckTyping — Proxy generator, attributes, and utilities.
- ExtensionMethods — Internal extension methods used across tracer.
- FaultTolerant — Retry/backoff/resiliency helpers.
- Generated — Generated sources (e.g., source generators output).
- Headers — HTTP header names and parsing helpers.
- HttpOverStreams — Stream-based HTTP to agent.
- DatabaseMonitoring — DBM helpers and settings.
- LibDatadog — P/Invoke and native bindings.
- Logging — Logger abstractions and initialization.
- PDBs — Symbol processing helpers.
- PlatformHelpers — OS/arch/runtime helpers.
- RemoteConfigurationManagement — RCM cache, protocols, and transport.
- ServiceFabric — Azure Service Fabric helpers.
- Util — Common utilities (time, concurrency, env, etc.).
- Vendors — Vendored dependencies (e.g., Newtonsoft patches) kept in sync.
