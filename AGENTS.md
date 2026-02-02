# Repository Guidelines

> **For AI Agents**: This file provides a navigation hub and quick reference. Each section includes "📖 Load when..." guidance to help you decide which detailed documentation files to load based on your current task.

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
- Loader/home: Build outputs publish a "monitoring home"; the native loader boots the tracer from there.
- Build system: Nuke coordinates .NET builds and CMake/vcpkg for native components.

## NuGet Package Architecture

### `Datadog.Trace` Package
The `Datadog.Trace` NuGet package provides the **manual instrumentation API** for customers:
- **Contains**: `Datadog.Trace.Manual.dll` - Public API for manual instrumentation
- **Does NOT contain**: Auto-instrumentation code or native profiler binaries
- **Usage**: Reference in application code for manual tracing (e.g., `Tracer.Instance.StartActive()`)

Auto-instrumentation comes from the tracer "monitoring home" deployed separately (via installers, MSI, container images, or specialized packages like `Datadog.AzureFunctions`).

### `Datadog.Trace.dll` vs `Datadog.Trace.Manual.dll`
- `Datadog.Trace.dll` - The full managed tracer with all auto-instrumentation code, loaded by the native profiler into instrumented processes
- `Datadog.Trace.Manual.dll` - Lightweight manual instrumentation API packaged in the `Datadog.Trace` NuGet package for customer reference

### Specialized Packages
- **Datadog.Trace.Bundle**: Complete bundle with managed/native libraries for all supported .NET runtimes, OS/arch combinations, and products (APM, ASM, Continuous Profiler). An alternative distribution mechanism for auto instrumentation
- **Datadog.AzureFunctions**: Leaner bundle for Azure Functions (see `docs/development/AzureFunctions.md`)
- Other serverless/platform-specific packages may bundle the tracer similarly

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

<details>
<summary>Detailed Tracer Structure (Tree View + Component Details)</summary>

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

**Component Details:**

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

</details>

## Build & Development

**Quick start:**
- Build: `./tracer/build.sh` (Linux/macOS) or `.\tracer\build.cmd` (Windows)
- Unit tests: `./tracer/build.sh BuildAndRunManagedUnitTests`
- Integration tests: `BuildAndRunLinuxIntegrationTests` / `BuildAndRunWindowsIntegrationTests` / `BuildAndRunOsxIntegrationTests`

📖 **Load when**: Setting up development environment, running builds, or troubleshooting build issues
- **`tracer/README.MD`** — Complete development setup guide (VS requirements, Docker, Dev Containers, platform-specific build commands, and Nuke targets)

## Creating Integrations

**Quick reference:**
- Location: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/<Area>/<Integration>.cs`
- Add `[InstrumentMethod]` attribute with assembly/type/method details and version range
- Implement `OnMethodBegin` and `OnMethodEnd`/`OnAsyncMethodEnd` handlers
- Use duck typing constraints (`where TReq : IMyShape, IDuckType`) or `obj.DuckCast<IMyShape>()` for third-party types
- Tests: Add under `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests` with samples in `tracer/test/test-applications/integrations`
- Generate boilerplate: `./tracer/build.ps1 RunInstrumentationGenerator`

📖 **Load when**: Creating a new integration or adding instrumentation to an existing library
- **`docs/development/AutomaticInstrumentation.md`** — Complete guide to creating integrations, CallTarget wiring, testing strategies, package version configuration, and CI testing

📖 **Load when**: Working with third-party types that can't be directly referenced or need version-agnostic access
- **`docs/development/DuckTyping.md`** — Duck typing patterns, proxy types, binding attributes, best practices, and performance benchmarks

## Azure Functions & Serverless

**Quick reference:**
- **Setup**: Use Azure App Services Site Extension on Windows Premium/Elastic Premium/Dedicated plans; use `Datadog.AzureFunctions` NuGet package for Linux Consumption/Container Apps
- **Tests**: `BuildAndRunWindowsAzureFunctionsTests` Nuke target; samples under `tracer/test/test-applications/azure-functions/`
- **External Repos**: [Azure Functions Host](https://github.com/Azure/azure-functions-host) and [.NET Worker](https://github.com/Azure/azure-functions-dotnet-worker)

📖 **Load when**: Working on Azure Functions instrumentation or debugging serverless issues
- **`docs/development/AzureFunctions.md`** — Setup, testing, instrumentation specifics, and debugging guide

📖 **Load when**: Need detailed architectural understanding of Azure Functions internals
- **`docs/development/for-ai/AzureFunctions-Architecture.md`** — Deep dive into Azure Functions Host and .NET Worker architecture, gRPC protocol, and instrumentation hook points

📖 **Load when**: Working on AWS Lambda or general serverless instrumentation
- **`docs/development/Serverless.md`** — Serverless instrumentation patterns across cloud providers

## Coding Standards

**C# style:**
- See `.editorconfig` (4-space indent, `System.*` first, prefer `var`). Types/methods PascalCase; locals camelCase
- Add missing `using` directives instead of fully-qualified type names
- Use modern C# syntax, but avoid features requiring types unavailable in older runtimes (e.g., no `ValueTuple` syntax for .NET Framework 4.6.1)
  - For instance, prefer `is not null` to `!= null` 
- Prefer modern collection expressions (`[]`)
- Use `StringUtil.IsNullOrEmpty()` instead of `string.IsNullOrEmpty()` for compatibility across all supported runtimes
- StyleCop: see `tracer/stylecop.json`; address warnings before pushing

**C/C++ style:**
- See `.clang-format`; keep consistent naming

## Windows Command Line Best Practices

**CRITICAL: Avoid `>nul` and `2>nul` redirections on Windows**

On Windows, redirecting to `nul` can create a literal file named "nul" instead of redirecting to the NUL device. These files are extremely difficult to delete and cause repository issues.

**Problem commands:**
```cmd
findstr /s /i "pattern" "*.cpp" "*.h" 2>nul
command 2>nul | head -20
any-command >nul
```

**Safe alternatives:**
1. **Don't suppress errors** - Let error output show naturally
2. **Use full device path**: `2>\\.\NUL` (works reliably but verbose)
3. **Use PowerShell** for cross-platform compatibility where applicable
4. **Prefer dedicated tools** over piped bash commands (use Grep, Glob, Read tools instead)

**Examples of safe patterns:**
```cmd
# Bad: Creates nul file
findstr /s /i "DD_TRACE" "*.cpp" 2>nul

# Good: Let errors show
findstr /s /i "DD_TRACE" "*.cpp"

# Good: Use full device path if suppression is essential
findstr /s /i "DD_TRACE" "*.cpp" 2>\\.\NUL
```

**Reference:** See https://github.com/anthropics/claude-code/issues/4928 for details on this Windows limitation.

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

The tracer runs in-process with customer applications and must have minimal performance impact.

**Critical code paths:**
1. **Bootstrap/Startup Code**: Managed loader, tracer initialization, static constructors, configuration loading, integration registration
2. **Hot Paths**: Span creation/tagging, context propagation, sampling decisions, instrumentation callbacks, request/response pipeline

**Key patterns:**
- **Zero-Allocation Provider Structs**: Use `readonly struct` with generic type parameters and interface constraints to avoid boxing
  - Example: `EnvironmentVariableProvider` in managed loader
- **Avoid Allocation in Logging**: Use format strings (`Log("value: {0}", x)`) instead of interpolation (`Log($"value: {x}")`)
- **Avoid params Array Allocations**: Provide overloads for common cases (0, 1, 2 args)

## Testing

**Frameworks:** xUnit (managed), GoogleTest (native)
**Test style:** Inline results in assertions: `SomeMethod().Should().Be(expected)`
**Docker:** Many integration tests require Docker; services in `docker-compose.yml`
**Filters:** `--filter "Category=Smoke"`, `--framework net6.0`

**Testing patterns:**
- Extract interfaces for environment/filesystem dependencies (e.g., `IEnvironmentVariableProvider`)
- Use struct implementations with generic constraints for zero-allocation production code
  - Example: Managed loader tests use `MockEnvironmentVariableProvider` (see `tracer/test/Datadog.Trace.Tests/ClrProfiler/Managed/Loader/`)
- Prefer using `[Theory]` with input data rather than duplicating tests

## Commit & Pull Request Guidelines

**Commits:**
- Imperative mood; optional scope tag (e.g., `fix(telemetry): …` or `[Debugger] …`)
- Reference issues when applicable
- Keep messages concise - avoid full diffs or extensive details

**Pull Requests:**
- Follow `.github/pull_request_template.md`
- Clear description, linked issues, risks/rollout notes
- Keep concise - essential context without excessive detail
- Focus on "what" and "why", brief "how" for complex changes
- Include tests/docs for changes
- CI: All checks must pass

## Documentation References

**Core docs:**
- `docs/README.md` — Overview and links
- `docs/CONTRIBUTING.md` — Contribution process and external PR policies
- `tracer/README.MD` — Dev setup, platform requirements, and build targets
- `docs/RUNTIME_SUPPORT_POLICY.md` — Supported runtimes

**Development guides:**
- `docs/development/AutomaticInstrumentation.md` — Creating integrations
- `docs/development/DuckTyping.md` — Duck typing guide
- `docs/development/AzureFunctions.md` — Azure Functions integration
- `docs/development/for-ai/AzureFunctions-Architecture.md` — Azure Functions architecture deep dive
- `docs/development/Serverless.md` — Serverless instrumentation
- `docs/development/UpdatingTheSdk.md` — SDK updates
- `docs/development/QueryingDatadogAPIs.md` — Querying Datadog APIs for debugging (spans, logs)

**CI & Testing:**
- `docs/development/CI/TroubleshootingCIFailures.md` — Investigating build/test failures in Azure DevOps
- `docs/development/CI/RunSmokeTestsLocally.md` — Running smoke tests locally

## Security & Configuration

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