# Repository Guidelines

> **For AI Agents**: This file provides a navigation hub and quick reference. Linked docs in each section can be loaded when their topic is relevant to your task.

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

The `Datadog.Trace` NuGet package ships **only** the manual instrumentation API (`Datadog.Trace.Manual.dll`) — **not** auto-instrumentation code or native profiler binaries. Reference it in customer code for `Tracer.Instance.StartActive()` etc.

The full managed tracer (`Datadog.Trace.dll`) contains all auto-instrumentation code and is delivered separately via the tracer "monitoring home" (installers, MSI, container images, or specialized packages: `Datadog.Trace.Bundle` for complete multi-runtime/multi-product distribution; `Datadog.AzureFunctions` for Azure Functions). The native profiler loads `Datadog.Trace.dll` into instrumented processes from the home.

## Tracer Structure

- `tracer/src/Datadog.Trace` — Core managed tracer library
  - `Activity` — System.Diagnostics.Activity bridge/helpers.
  - `Agent` — Agent transport, payloads, health, serialization.
  - `AppSec` — Application Security (WAF/RASP) components.
  - `AspNet` — ASP.NET helpers/back-compat.
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
  - `FaultTolerant` — Retry/backoff/resiliency helpers.
  - `Generated` — Generated sources (source generators output).
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

## Build & Development

**Quick start:**
- Build: `./tracer/build.sh` (Linux/macOS) or `.\tracer\build.cmd` (Windows)
- Unit tests: `./tracer/build.sh BuildAndRunManagedUnitTests`
- Integration tests: `BuildAndRunIntegrationTests`

- **`tracer/README.md`** — Complete development setup guide (VS requirements, Docker, Dev Containers, platform-specific build commands, and Nuke targets)

## Creating Integrations

**Quick reference:**
- Location: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/<Area>/<Integration>.cs`
- Add `[InstrumentMethod]` attribute with assembly/type/method details and version range
- Implement `OnMethodBegin` and `OnMethodEnd`/`OnAsyncMethodEnd` handlers
- Use duck typing constraints (`where TReq : IMyShape, IDuckType`) or `obj.DuckCast<IMyShape>()` for third-party types
- Tests: Add under `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests` with samples in `tracer/test/test-applications/integrations`
- Generate boilerplate (GUI): `./tracer/build.ps1 RunInstrumentationGenerator`
- Generate boilerplate (CLI): `./tracer/build.ps1 RunInstrumentationGeneratorCli --assembly-path <dll> --type-name <type> --method-name <method>`

- **`docs/development/AutomaticInstrumentation.md`** — Complete guide to creating integrations, CallTarget wiring, testing strategies, package version configuration, and CI testing
- **`docs/development/InstrumentationGenerator.md`** — GUI and CLI instrumentation generator tools, Nuke integration, duck typing flags, JSON output, and two-tool workflow with dotnet-inspect

- **`docs/development/DuckTyping.md`** — Duck typing patterns, proxy types, binding attributes, best practices, and performance benchmarks

## Azure Functions & Serverless

**Quick reference:**
- **Setup**: Use Azure App Services Site Extension on Windows Premium/Elastic Premium/Dedicated plans; use `Datadog.AzureFunctions` NuGet package for Linux Consumption/Container Apps
- **Tests**: `BuildAndRunWindowsAzureFunctionsTests` Nuke target; samples under `tracer/test/test-applications/azure-functions/`
- **External Repos**: [Azure Functions Host](https://github.com/Azure/azure-functions-host) and [.NET Worker](https://github.com/Azure/azure-functions-dotnet-worker)

- **`docs/development/AzureFunctions.md`** — Setup, testing, instrumentation specifics, and debugging guide

- **`docs/development/for-ai/AzureFunctions-Architecture.md`** — Deep dive into Azure Functions Host and .NET Worker architecture, gRPC protocol, and instrumentation hook points

- **`docs/development/AwsLambdaIntegrationTests.md`** — AWS Lambda integration test setup, architecture, and test patterns

## Coding Standards

**C# style:**
- See `.editorconfig` (auto-enforced)
- Add missing `using` directives instead of fully-qualified type names
- Use modern C# syntax, but avoid features requiring types unavailable in older runtimes (e.g., no `ValueTuple` syntax for .NET Framework 4.6.1)
  - For instance, prefer `is not null` to `!= null`
- Prefer modern collection expressions (`[]`)
- Use `StringUtil.IsNullOrEmpty()` instead of `string.IsNullOrEmpty()` for compatibility across all supported runtimes
- StyleCop: see `tracer/stylecop.json`; address warnings before pushing
- Never manually edit generated files (`.g.` in the file extension). Read the file header for regeneration instructions instead.

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

### Log Argument Formatting

Never use `ToString()` on numeric types in log calls - use generic log methods instead:
```csharp
// BAD - allocates a string unnecessarily
Log.Debug(ex, "Error (attempt {Attempt})", (attempt + 1).ToString());

// GOOD - uses generic method, no allocation
Log.Debug<int>(ex, "Error (attempt {Attempt})", attempt + 1);
```

### Log Levels for Retry Operations

When implementing retry logic, use appropriate log levels:
- **Debug**: Intermediate retry attempts (transient errors are expected)
- **Error**: Final failure after all retries exhausted
- **Error**: Non-retryable errors (e.g., 400 Bad Request indicates a bug)

### ErrorSkipTelemetry Usage

`Log.ErrorSkipTelemetry` logs locally but does NOT send to Datadog telemetry. Use it for:
- **Expected environmental errors**: Network connectivity issues, endpoint unavailability
- **Transient failures**: Errors that are expected in production and self-resolve

**Do NOT use ErrorSkipTelemetry for:**
- Errors in outer catch blocks that would only catch unexpected exceptions
- HTTP 400 Bad Request (indicates a bug in our payload)
- Errors that indicate bugs in the tracer code

**Understanding code flow is critical**: If inner methods already handle expected errors, outer catch blocks should use `Log.Error` since they would only catch unexpected exceptions (bugs).

### Error Messages for Network Failures

When logging final failures for network operations, include:
1. The endpoint that failed
2. Number of attempts made
3. Link to troubleshooting documentation

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

**Testing patterns:**
- Extract interfaces for environment/filesystem dependencies (e.g., `IEnvironmentVariableProvider`)
- Use struct implementations with generic constraints for zero-allocation production code
  - Example: Managed loader tests use `MockEnvironmentVariableProvider` (see `tracer/test/Datadog.Trace.Tests/ClrProfiler/Managed/Loader/`)
- Prefer using `[Theory]` with input data rather than duplicating tests

- **`docs/development/TracerDebugging.md`** — Local debugging techniques, launchSettings.json configuration, $(SolutionDir) path issues, IDE-specific tips, and troubleshooting common tracer loading problems

## Commit & Pull Request Guidelines

- Commits: imperative mood, optional `[Area]` prefix (e.g. `[Debugger]`, `[SymDB]`). Keep messages concise — avoid full diffs or extensive explanation.
- PRs: follow [`.github/pull_request_template.md`](.github/pull_request_template.md). Keep descriptions concise — focus on "what" and "why", brief "how" only when complex.

## Documentation References

**Core docs:**
- `docs/README.md` — Overview and links
- `docs/CONTRIBUTING.md` — Contribution process and external PR policies
- `tracer/README.md` — Dev setup, platform requirements, and build targets
- `docs/RUNTIME_SUPPORT_POLICY.md` — Supported runtimes

**Development guides:**
- `docs/development/AutomaticInstrumentation.md` — Creating integrations
- `docs/development/InstrumentationGenerator.md` — GUI and CLI instrumentation generator tools
- `docs/development/DuckTyping.md` — Duck typing guide
- `docs/development/TracerDebugging.md` — Local debugging, IDE configuration, path issues, and troubleshooting
- `docs/development/AzureFunctions.md` — Azure Functions integration
- `docs/development/for-ai/AzureFunctions-Architecture.md` — Azure Functions architecture deep dive
- `docs/development/AwsLambdaIntegrationTests.md` — AWS Lambda integration tests
- `docs/development/UpdatingTheSdk.md` — SDK updates
- `docs/development/QueryingDatadogAPIs.md` — Querying Datadog APIs for debugging (spans, logs)

**CI & Testing:**
- `docs/development/CI/TroubleshootingCIFailures.md` — Investigating build/test failures in Azure DevOps
- `docs/development/CI/RunSmokeTestsLocally.md` — Running smoke tests locally

## Configuration

- **`tracer/src/Datadog.Trace/Configuration/supported-configurations.yaml`** — Human-readable config metadata, product categorization, key aliases, deprecations, and default values for all `DD_*` and `OTEL_*` environment variables (also consumed by source generators).

- **`docs/development/Configuration/AddingConfigurationKeys.md`** — Step-by-step guide for adding config keys: YAML definitions, source generators, aliases, telemetry normalization, and related analyzers

## Security & Configuration

- Do not commit secrets; prefer env vars (`DD_*`). `.env` should not contain credentials.
- Use `global.json` SDK; confirm with `dotnet --version`.

## Glossary

Common acronyms used in this repository:

- **AAS** — Azure App Services
- **AAP** — App and API Protection (formerly ASM, previously AppSec)
- **AOT** — Ahead-of-Time (compilation)
- **APM** — Application Performance Monitoring
- **ASM** — see AAP
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