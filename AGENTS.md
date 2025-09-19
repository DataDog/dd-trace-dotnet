# Repository Guidelines

## Project Structure & Module Organization

- tracer/src — Managed tracer, analyzers, tooling.
- tracer/test — Unit/integration tests; sample apps under test/test-applications.
- profiler/src, profiler/test — Native profiler and tests.
- shared — Cross-cutting native libs/utilities.
- docs — Product and developer docs.
- docker-compose.yml — Test dependencies (databases, brokers, etc.).
- Solutions: `Datadog.Trace.sln`, `Datadog.Profiler.sln` (IDE).

## Architecture Overview

- Auto-instrumentation: Native CLR profiler hooks the runtime (CallTarget) and loads the managed tracer.
- Managed tracer: `Datadog.Trace` handles spans, context propagation, and library integrations.
- Loader/home: Build outputs publish a “monitoring home”; the native loader boots the tracer from there.
- Build system: Nuke coordinates .NET builds and CMake/vcpkg for native components.

## Tracer Structure

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

### Tracer Structure (Detailed)

Tree Overview

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

- ClrProfiler — Auto-instrumentation runtime
  - AutoInstrumentation — Integrations grouped by tech (AWS, AdoNet, AspNet/AspNetCore, Azure, Couchbase, Elasticsearch, GraphQL, Grpc, Http, IbmMq, Kafka, Logging, MongoDb, Msmq, OpenTelemetry, Process, Protobuf, RabbitMQ, Redis, Remoting, RestSharp, Testing, TraceAnnotations, Wcf).
  - CallTarget — Invoker, handlers, state structs, async continuations and helpers.
  - Helpers — IL/interop helpers; native definitions and memory helpers.
  - ServerlessInstrumentation — Serverless-specific hooks.
- Agent — Client and transports to the Datadog Agent
  - DiscoveryService — Detect agent endpoints/capabilities.
  - MessagePack — Trace payload encoding/formatting.
  - StreamFactories — HTTP/Unix/Windows stream implementations.
  - TraceSamplers — Client-side sampling strategies.
  - Transports — HTTP/pipes transport strategies and tuning.
- Configuration — Settings and sources
  - ConfigurationSources — Env vars, JSON, args, RCM providers.
  - Schema — Span attribute schema configuration.
  - Core — `TracerSettings`, `ExporterSettings`, `IntegrationSettings`, git metadata providers.
- Propagators — Context injection/extraction
  - Datadog, W3C (tracecontext/baggage), B3 (single/multiple header), factories/utilities.
- Telemetry — Product telemetry
  - Collectors — Feature/runtime collectors and samplers.
  - DTOs — Payload models and envelopes.
  - Metrics — Counters and series.
  - Transports — HTTP transport implementations and headers.
- Debugger — Dynamic Instrumentation (probes/snapshots)
  - Instrumentation, Snapshots, Upload, Caching, Configurations, Expressions, PInvoke, Symbols, ExceptionAutoInstrumentation, RateLimiting, Sink, SpanCodeOrigin.
- Iast — Interactive Application Security Testing
  - Aspects (sources/sinks), Dataflow (taint tracking), Propagation, SensitiveData, Settings, Telemetry, Analyzers, Helpers.
- DataStreamsMonitoring — DSM checkpoints and pathway context
  - Aggregation, Transport, Hashes, Utils; manager/writer and context propagator.
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

## Build, Test, and Development Commands

- Build tracer home (default): `./tracer/build.sh` or `powershell ./tracer/build.ps1`.
- Show config: `./tracer/build.sh Info`.
- Unit tests (managed): `./tracer/build.sh BuildAndRunManagedUnitTests`.
- Unit tests (native): `./tracer/build.sh RunNativeUnitTests`.
- Integration tests: Linux `BuildAndRunLinuxIntegrationTests`, macOS `BuildAndRunOsxIntegrationTests`, Windows `BuildAndRunWindowsIntegrationTests`.
- Package artifacts: `./tracer/build.sh PackageTracerHome`.
- Coverage: append `--code-coverage-enabled true`.

## Nuke Targets

- Usage: `./tracer/build.sh <Target> [--option value]` (Windows: `powershell ./tracer/build.ps1 <Target>`). List all with `--help`.
- Core
  - `Info` — Print current build config.
  - `Clean` — Remove build outputs/obj/bin.
  - `BuildTracerHome` — Build native + managed tracer and publish monitoring home (default).
  - `BuildProfilerHome` — Build/provision profiler artifacts.
  - `BuildNativeLoader` — Build and publish native loader.
  - `PackNuGet` / `PackageTracerHome` — Create NuGet/MSI/zip artifacts.
- Tests (managed/native)
  - `BuildAndRunManagedUnitTests` — Build and run managed unit tests.
  - `RunNativeUnitTests` — Build and run native unit tests.
  - `RunIntegrationTests` — Execute integration tests (used by OS-specific wrappers below).
  - Debugger: `BuildAndRunDebuggerIntegrationTests`.
- Platform wrappers
  - Windows: `BuildAndRunWindowsIntegrationTests`, `BuildAndRunWindowsRegressionTests`, `BuildAndRunWindowsAzureFunctionsTests`.
  - Linux: `BuildAndRunLinuxIntegrationTests`.
  - macOS: `BuildAndRunOsxIntegrationTests`.
- Profiler & Native
  - `CompileProfilerNativeSrc`, `CompileProfilerNativeTests`, `RunProfilerNativeUnitTests{Windows|Linux}`.
  - Static analysis: `RunClangTidyProfiler{Windows|Linux}`, `RunCppCheckProfiler{Windows|Linux}`.
  - Native loader tests: `CompileNativeLoader*`, `RunNativeLoaderTests{Windows|Linux}`.
- Tools & Utilities
  - `BuildRunnerTool`, `PackRunnerToolNuget`, `BuildStandaloneTool`, `InstallDdTraceTool`.
  - `RunBenchmarks` — Execute performance benchmarks.
  - `UpdateSnapshots`, `PrintSnapshotsDiff` — Snapshot testing utilities.
  - `UpdateVersion`, `GenerateSpanDocumentation`, `RunInstrumentationGenerator` — Maintenance.

## macOS Development

- Prereqs: Install .NET SDK, Xcode Command Line Tools, and `cmake` (`brew install cmake`).
- Build: `./tracer/build.sh Clean BuildTracerHome`.
- Unit tests: `./tracer/build.sh BuildAndRunManagedUnitTests BuildAndRunNativeUnitTests`.
- Integration tests: `docker-compose up StartDependencies.OSXARM64`; run `./tracer/build.sh BuildAndRunOsxIntegrationTests`; `docker-compose down` to stop.
- Filters as needed: `--framework net6.0` and `--filter "Category=Smoke"`.
- Apple Silicon: Some services are x86-only; see `docker-compose.yml` comments. Consider Colima if you need amd64 containers.
- Details: see `tracer/README.MD` (macOS section).

## Coding Style & Naming Conventions

- C#: .editorconfig (4-space indent, System.* first, prefer var). Types/methods PascalCase; locals camelCase.
- StyleCop: `tracer/stylecop.json`; address warnings before pushing.
- C/C++: `.clang-format`; keep consistent naming.

## Testing Guidelines

- Frameworks: xUnit (managed), GoogleTest (native).
- Projects: `*.Tests.csproj` under `tracer/test`, native under `profiler/test`.
- Filters: `--filter "Category=Smoke"`, `--framework net6.0` as needed.
- Docker: Many integration tests require Docker; services in `docker-compose.yml`.

## Commit & Pull Request Guidelines

- Commits: Imperative; optional scope tag (e.g., `fix(telemetry): …` or `[Debugger] …`); reference issues.
- PRs: Clear description, linked issues, risks/rollout, screenshots/logs if behavior changes.
- CI: All checks green; include tests/docs for changes.

## Internal Docs & References

- docs/README.md — Overview and links
- docs/CONTRIBUTING.md — Contribution process
- tracer/README.MD — Dev setup and targets
- docs/development/AutomaticInstrumentation.md — Adding integrations
- docs/development/DuckTyping.md — Duck typing guide
- docs/development/CI/RunSmokeTestsLocally.md — Smoke tests locally
- docs/development/UpdatingTheSdk.md — SDK updates
- docs/RUNTIME_SUPPORT_POLICY.md — Supported runtimes

## CallTarget Wiring

- Define an integration class decorated with `InstrumentMethod` describing the target assembly/type/method, version range, and integration name. Example: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Couchbase/ClusterNodeIntegration.cs:1` and attribute API in `tracer/src/Datadog.Trace/ClrProfiler/InstrumentMethodAttribute.cs:1`.
- Build collects attributes and generates native definitions used by the CLR profiler (see generator `tracer/build/_build/CodeGenerators/CallTargetsGenerator.cs:560`). This emits a C++ list (registered at startup) and a JSON snapshot.
- Native CLR profiler (C++) registers those definitions and rewrites IL for matched methods during JIT/ReJIT to call the managed invoker.
- The managed entry point is `CallTargetInvoker` (`tracer/src/Datadog.Trace/ClrProfiler/CallTarget/CallTargetInvoker.cs:1`). It invokes `OnMethodBegin` and `OnMethodEnd`/`OnAsyncMethodEnd` on your integration type, handling generics, ref/out, and async continuations.
- `OnMethodBegin` returns `CallTargetState`, which can contain a tracing `Scope` to represent the span. That state flows to the end handler; async returns are awaited and then `OnAsyncMethodEnd` runs.
- Integrations typically create a scope in `OnMethodBegin` and tag/finish it in the end handler. See the Couchbase example’s `OnMethodBegin`/`OnAsyncMethodEnd` methods.
- Enable/disable per-integration via config (IntegrationName), and by framework/versions declared in the attribute.
- For a full walkthrough and patterns, read `docs/development/AutomaticInstrumentation.md`.

## Duck Typing Mechanism

- Purpose: Interact with external types across versions without adding hard dependencies. Provides fast, strongly-typed access via generated proxies.
- Shape interfaces: Define minimal contracts (properties/methods) you need, e.g., `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AWS/Kinesis/IPutRecordsRequest.cs:1` and `IAmazonKinesisRequestWithStreamName.cs:1`.
- Interface + IDuckType constraints: In CallTarget integrations, use `where TReq : IMyShape, IDuckType`. Example: `PutRecordsIntegration.OnMethodBegin` in `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AWS/Kinesis/PutRecordsIntegration.cs:1`.
- Creating proxies:
  - Generic constraints (above) let the woven callsite pass a proxy automatically.
  - At runtime by type: `DuckType.GetOrCreateProxyType(typeof(IMyShape), targetType)` and `CreateInstance(...)` (see `tracer/src/Datadog.Trace/OTelMetrics/OtlpMetricsExporter.cs:1`).
  - From an instance: `obj.DuckCast<IMyShape>()` for nested values (see `GetRecordsIntegration` `DuckCast<IRecord>` in `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AWS/Kinesis/GetRecordsIntegration.cs:1`).
- Accessing originals: All proxies implement `IDuckType` (`tracer/src/Datadog.Trace/DuckTyping/IDuckType.cs:1`) exposing `Instance` and `Type`.
- Binding rules: Name/signature matching; support for properties/fields/methods. Use attributes in `tracer/src/Datadog.Trace/DuckTyping/` to control binding:
  - `[DuckField]`, `[DuckPropertyOrField]`, `[DuckIgnore]`, `[DuckInclude]`, `[DuckReverseMethod]`, `[DuckCopy]`, `[DuckAsClass]`, `[DuckType]`, `[DuckTypeTarget]`.
- Visibility: Proxies can access non-public members; the library emits IL and uses `IgnoresAccessChecksToAttribute` if present (`tracer/src/Datadog.Trace/DuckTyping/IgnoresAccessChecksToAttribute.cs:1`).
- Nullability/perf: Enable `#nullable` in new files; proxies are cached per (shape,target) for low overhead. See core implementation `tracer/src/Datadog.Trace/DuckTyping/DuckType.cs:1` and partials.
- Guidance: Prefer interface shapes; avoid vendor types in signatures; check for `proxy.Instance != null` before use; keep shapes stable across upstream versions. Deep dive: `docs/development/DuckTyping.md`.

## Integrations

- Location: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/<Area>/<Integration>.cs`. Place shape interfaces and shared helpers in the same `<Area>` folder (e.g., `AWS/Kinesis/*`, `Couchbase/*`, `GraphQL/*`).
- Create one
  - Add shape interfaces for third‑party types you consume (no direct package refs).
  - Add an integration class with one or more `InstrumentMethod` attributes specifying: `AssemblyName`, `TypeName`, `MethodName`, `ReturnTypeName`, `ParameterTypeNames`, `MinimumVersion`, `MaximumVersion`, `IntegrationName` (and `CallTargetIntegrationKind` if needed).
  - Implement static handlers: `OnMethodBegin` returns `CallTargetState`; end handlers are `OnMethodEnd` for sync or `OnAsyncMethodEnd` for async. Use `Tracer.Instance` to create a `Scope`, tag it, and dispose in the end handler.
  - Use duck typing in method generics: `where TReq : IMyShape, IDuckType` and `DuckCast<TShape>()` for nested members. Examples: `AWS/Kinesis/PutRecordsIntegration.cs:1`, `Couchbase/ClusterNodeIntegration.cs:1`.
- Build/registration: Definitions are discovered and generated during build; no manual native changes required.
- Tests: Add tests under `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests` and corresponding samples under `tracer/test/test-applications/integrations`. Run with OS‑specific Nuke targets; filter with `--filter`/`--framework`.

## Security & Configuration Tips

- Do not commit secrets; prefer env vars (`DD_*`). `.env` should not contain credentials.
- Use `global.json` SDK; confirm with `dotnet --version`.
