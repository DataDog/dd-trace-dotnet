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

- C#: see `.editorconfig` (4-space indent, `System.*` first, prefer `var`). Types/methods PascalCase; locals camelCase.
  - When a "using" directive is missing in a file, add it instead of using fully-qualified type names.
  - Use modern C# syntax, but avoid syntax that requires types not available in older runtimes (for example, don't use syntax that requires ValueTuple because is not included in .NET Framework 4.6.1)
  - Prefer modern collection expressions (`[]`)
- StyleCop: see `tracer/stylecop.json`; address warnings before pushing.
- C/C++: see `.clang-format`; keep consistent naming.

## Logging Guidelines

Use clear, customer-facing terminology in log messages to avoid confusion. `Profiler` is ambiguous—it can refer to the .NET profiling APIs we use internally or the Continuous Profiler product.

**Customer-facing terminology (high-level logs):**
- **Datadog SDK** — When disabling the entire product or referring to the whole monitoring solution
  - Example: `"The Datadog SDK has been disabled"`
- **Instrumentation** or **Instrumentation component** — For the native tracer auto-instrumentation
  - Example: `"Instrumentation has been disabled"` or `"The Instrumentation component failed to initialize"`
- **Continuous Profiler** — Always use full name for the profiling product
  - Example: `"The Continuous Profiler has been disabled"`
- **Datadog.Trace.dll** — For the managed tracer assembly (avoid "managed profiler")
  - Example: `"Unable to initialize: Datadog.Trace.dll was not yet loaded into the App Domain"`

**Internal/technical naming (still valid):**
- Native loader, Native tracer, Managed tracer loader, Managed tracer, Libdatadog, Continuous Profiler
- `CorProfiler` / `ICorProfiler` / `COR Profiler` for runtime components

**Reference:** See PR 7467 for examples of consistent terminology in native logs.

## Performance Guidelines

- Minimize heap allocations: The tracer runs in-process with customer applications and must have minimal performance impact. Avoid unnecessary object allocations, prefer value types where appropriate, use object pooling for frequently allocated objects, and cache reusable instances.

### Performance-Critical Code Paths

Performance is especially critical in two areas:

1. **Bootstrap/Startup Code**: Initialization code runs at application startup in every instrumented process, including:
   - The managed loader (`Datadog.Trace.ClrProfiler.Managed.Loader`)
   - Tracer initialization in `Datadog.Trace` (static constructors, configuration loading, first tracer instance creation)
   - Integration registration and setup

   Any allocations or inefficiencies here directly impact application startup time and customer experience. This code must be extremely efficient.

2. **Hot Paths**: Code that executes frequently during application runtime, such as:
   - Span creation and tagging (executes on every traced operation)
   - Context propagation (executes on every HTTP request/response)
   - Sampling decisions (executes on every trace)
   - Integration instrumentation callbacks (executes on every instrumented method call)
   - Any code in the request/response pipeline

In these areas, even small inefficiencies are multiplied by the frequency of execution and can significantly impact application performance.

### Performance Optimization Patterns

**Zero-Allocation Provider Structs**
- Use `readonly struct` implementing interfaces instead of classes for frequently-instantiated abstractions
- Use generic type parameters with interface constraints to avoid boxing: `<TProvider> where TProvider : IProvider`
- Example: `EnvironmentVariableProvider` in managed loader (see tracer/src/Datadog.Trace.ClrProfiler.Managed.Loader)
- Benefits: Zero heap allocations, no boxing, better JIT optimization

**Avoid Allocation in Logging**
- Use format strings (`Log("value: {0}", x)`) instead of interpolation (`Log($"value: {x}")`)
- Allows logger to check level before formatting
- Critical in startup and hot paths where logging is frequent

**Avoid params Array Allocations**
- Provide overloads for common cases (0, 1, 2 args) that avoid `params object?[]` array allocation
- Keep params overload as fallback for 3+ args
- Example: Logging methods with multiple overloads for different argument counts

## Testing Guidelines

- Frameworks: xUnit (managed), GoogleTest (native).
- Projects: `*.Tests.csproj` under `tracer/test`, native under `profiler/test`.
- Filters: `--filter "Category=Smoke"`, `--framework net6.0` as needed.
- Docker: Many integration tests require Docker; services in `docker-compose.yml`.
- Test style: Inline result variables in assertions. Prefer `SomeMethod().Should().Be(expected)` over storing intermediate `result` variables.

### Testing Patterns

**Abstraction for Testability**
- Extract interfaces for environment/filesystem dependencies (e.g., `IEnvironmentVariableProvider`)
- Allows mocking in unit tests without affecting production performance
- Use struct implementations with generic constraints for zero-allocation production code
- Example: Managed loader tests use `MockEnvironmentVariableProvider` for isolation (see tracer/test/Datadog.Trace.Tests/ClrProfiler/Managed/Loader/)

## Commit & Pull Request Guidelines

- Commits: Imperative; optional scope tag (e.g., `fix(telemetry): …` or `[Debugger] …`); reference issues. Keep messages concise - avoid including full diffs or extensive details in the commit message.
- PRs: Clear description, linked issues, risks/rollout, screenshots/logs if behavior changes.
  - Follow the existing PR description template in `.github/pull_request_template.md`
  - Keep descriptions concise - provide essential context without excessive detail
  - Focus on "what" and "why", with brief "how" for complex changes
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

## CallTarget Wiring & Integrations

Creating new integrations involves CallTarget instrumentation and duck typing. For full details, see:
- `docs/development/AutomaticInstrumentation.md` — Complete guide to creating integrations, testing, and rollout
- `docs/development/DuckTyping.md` — Duck typing patterns, best practices, and performance considerations

Quick reference:
- Location: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/<Area>/<Integration>.cs`
- Add `[InstrumentMethod]` attribute with assembly/type/method details and version range
- Implement `OnMethodBegin` and `OnMethodEnd`/`OnAsyncMethodEnd` handlers
- Use duck typing constraints (`where TReq : IMyShape, IDuckType`) or `obj.DuckCast<IMyShape>()` for third-party types
- Tests: Add under `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests` with corresponding samples in `tracer/test/test-applications/integrations`
- Run `./tracer/build.ps1 RunInstrumentationGenerator` to generate boilerplate code

## Azure Functions

For detailed information on Azure Functions integration (in-process vs isolated worker models, instrumentation specifics, ASP.NET Core integration, and debugging), see `docs/development/AzureFunctions.md`.

Quick reference:
- **Setup**: Use Azure App Services Site Extension on Windows Premium/Elastic Premium/Dedicated plans; use `Datadog.AzureFunctions` NuGet package for Linux Consumption/Container Apps
- **Tests**: `BuildAndRunWindowsAzureFunctionsTests` Nuke target; samples under `tracer/test/test-applications/azure-functions/`
- **Dependencies**: `Datadog.AzureFunctions` → `Datadog.Serverless.Compat` ([datadog-serverless-compat-dotnet](https://github.com/DataDog/datadog-serverless-compat-dotnet)) contains agent executable

## Security & Configuration Tips

- Do not commit secrets; prefer env vars (`DD_*`). `.env` should not contain credentials.
- Use `global.json` SDK; confirm with `dotnet --version`.

## Glossary

Common acronyms used in this repository:

- **AAS** — Azure App Services
- **AAP** — App and API Protection (formerly ASM, previously AppSec)
- **AOT** — Ahead-of-Time (compilation)
- **APM** — Application Performance Monitoring
- **ASM** — Application Security Management (formerly AppSec; now AAP)
- **CI** — Continuous Integration / CI Visibility
- **CP** — Continuous Profiler
- **DBM** — Database Monitoring
- **DI** — Dynamic Instrumentation
- **DSM** — Data Streams Monitoring
- **IAST** — Interactive Application Security Testing
- **JIT** — Just-in-Time (compiler)
- **OTEL** — OpenTelemetry
- **R2R** — ReadyToRun
- **RASP** — Runtime Application Self-Protection
- **RCM** — Remote Configuration Management
- **RID** — Runtime Identifier
- **TFM** — Target Framework Moniker
- **WAF** — Web Application Firewall
