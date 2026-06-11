# Repository Guidelines

> **For AI Agents**: This file is a navigation hub and quick reference. Load the linked docs in each section when their topic is relevant to your task.

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

`tracer/src/Datadog.Trace` — core managed tracer. Notable subfolders (names are self-descriptive; a few clarified below):

- `Agent` (transport/payloads), `ClrProfiler` (auto-instrumentation: CallTarget, handlers, definitions), `Configuration` (settings/sources/env parsing), `DuckTyping`, `Propagators` (Datadog/W3C/B3 inject/extract), `Sampling`, `Processors` (span pipelines), `Tagging`, `Telemetry`, `Logging`, `Util`, `Vendors` (vendored third-party).
- Products: `AppSec` (WAF/RASP), `Iast`, `Ci` (CI Visibility), `Debugger` (Dynamic Instrumentation), `ContinuousProfiler`, `DataStreamsMonitoring`, `DatabaseMonitoring`, `RuntimeMetrics`, `RemoteConfigurationManagement`.
- Interop/bridges: `Activity`, `OpenTelemetry`/`OTelMetrics`, `DiagnosticListeners`, `DogStatsd`, `HttpOverStreams` (agent transport), `LibDatadog` (native interop), `PlatformHelpers`.

Other modules under `tracer/src`:

- `Datadog.Trace.ClrProfiler.Managed.Loader` — Managed bootstrapper loaded by the profiler.
- `Datadog.Trace.Manual` — Manual instrumentation shims/APIs.
- `Datadog.Trace.SourceGenerators` — Compile-time code generators.
- `Datadog.Trace.OpenTracing`, `Datadog.Trace.MSBuild`, `Datadog.Trace.Trimming`, `Datadog.AzureFunctions`, `Datadog.FleetInstaller`.
- `Datadog.Trace.Tools.*` — CLI tools, analyzers, shared libs, and dd_dotnet.
- `Datadog.InstrumentedAssembly*` — Pre-instrumented assembly tooling/verification.
- `Datadog.AutoInstrumentation.Generator` — Instrumentation metadata generators.
- `Datadog.Tracer.Native` — Native interop glue and packaging metadata.

## Build & Development

- Build: `./tracer/build.sh` (Linux/macOS) or `.\tracer\build.cmd` (Windows)
- Unit tests: `./tracer/build.sh BuildAndRunManagedUnitTests`
- Integration tests: `BuildAndRunIntegrationTests`
- **`tracer/README.md`** — Full dev setup (VS requirements, Docker, Dev Containers, platform build commands, Nuke targets).

## Creating Integrations

- Location: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/<Area>/<Integration>.cs`
- Add `[InstrumentMethod]` attribute with assembly/type/method details and version range.
- Implement `OnMethodBegin` and `OnMethodEnd`/`OnAsyncMethodEnd` handlers.
- Use duck typing constraints (`where TReq : IMyShape, IDuckType`) or `obj.DuckCast<IMyShape>()` for third-party types.
- Tests: under `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests`; samples in `tracer/test/test-applications/integrations`.
- Generate boilerplate: `./tracer/build.ps1 RunInstrumentationGenerator`.
- **`docs/development/AutomaticInstrumentation.md`** — CallTarget wiring, testing, package versions, CI testing. **`docs/development/DuckTyping.md`** — proxy types, binding attributes, performance.

## Azure Functions & Serverless

- **Setup**: Azure App Services Site Extension on Windows Premium/Elastic Premium/Dedicated plans; `Datadog.AzureFunctions` NuGet package for Linux Consumption/Container Apps.
- **Tests**: `BuildAndRunWindowsAzureFunctionsTests` Nuke target; samples under `tracer/test/test-applications/azure-functions/`.
- Docs: **`docs/development/AzureFunctions.md`** (setup/testing/debugging), **`docs/development/for-ai/AzureFunctions-Architecture.md`** (Host + .NET Worker, gRPC, hook points), **`docs/development/AwsLambdaIntegrationTests.md`** (Lambda tests).

## Coding Standards

**C#:**
- `.editorconfig` is auto-enforced; StyleCop per `tracer/stylecop.json` — address warnings before pushing.
- Add missing `using` directives instead of fully-qualified type names.
- Use modern C# (`is not null`, collection expressions `[]`) but avoid features needing types unavailable on older runtimes (e.g., no `ValueTuple` syntax for .NET Framework 4.6.1).
- Use `StringUtil.IsNullOrEmpty()` instead of `string.IsNullOrEmpty()` for cross-runtime compatibility.
- Never manually edit generated files (`.g.` in the extension) — read the header for regeneration instructions.

**C/C++:** See `.clang-format`; keep consistent naming.

## Windows Command Line Best Practices

**Avoid `>nul` / `2>nul` redirections.** On Windows these can create a literal file named `nul` (instead of using the NUL device) that is extremely hard to delete and corrupts the repo. Instead:

- Don't suppress errors — let them show naturally.
- If suppression is essential, use the full device path: `2>\\.\NUL`.
- Prefer the dedicated Grep/Glob/Read tools over piped shell commands.

## Logging Guidelines

Keep logs short and consistent — prefer the single word **Profiler** for everything Datadog-related so messages stay terse:

- **Profiler** — the whole product. When it's turned off at startup, log `"The Profiler has been disabled"`.
- **managed profiler** — the managed tracer assembly (`Datadog.Trace.dll`); call it the "managed profiler" in user-facing logs.
- **profiler** — fine as shorthand for the Continuous Profiler product too; no need to spell out the full name.
- Avoid "Datadog SDK" and "Instrumentation component" — they're too vague/wordy for customers.

Internal/technical naming still valid: Native loader/tracer, Managed tracer loader, Managed tracer, Libdatadog, `CorProfiler`/`ICorProfiler`/`COR Profiler`.

**Argument formatting:** Never `ToString()` numeric types in log calls — use generic overloads to avoid allocation: `Log.Debug<int>(ex, "Error (attempt {Attempt})", attempt + 1)` (not `(attempt + 1).ToString()`).

**Retry log levels:** Debug for intermediate retry attempts (transient errors expected); Error for final failure after retries exhausted and for non-retryable errors (e.g. HTTP 400, indicating a bug).

**`Log.ErrorSkipTelemetry`** logs locally but skips Datadog telemetry. Use for expected environmental/transient errors (network connectivity, endpoint unavailability). Do NOT use it for outer catch blocks (which catch unexpected bugs), HTTP 400, or other bug-indicating errors — those should use `Log.Error`. Understand the code flow: if inner methods already handle expected errors, the outer catch only sees bugs.

**Network failure messages:** include the failed endpoint, number of attempts, and a link to troubleshooting docs.

## Performance Guidelines

The tracer runs in-process with customer apps and must have minimal impact.

**Critical paths:** bootstrap/startup (managed loader, tracer init, static constructors, config loading, integration registration) and hot paths (span creation/tagging, context propagation, sampling, instrumentation callbacks, request/response pipeline).

**Key patterns:**
- **Zero-allocation provider structs:** `readonly struct` with generic type params + interface constraints to avoid boxing (e.g. `EnvironmentVariableProvider` in the managed loader).
- **Avoid allocation in logging:** format strings (`Log("value: {0}", x)`) not interpolation.
- **Avoid params array allocations:** provide overloads for 0/1/2 args.

## Testing

- **Frameworks:** xUnit (managed), GoogleTest (native). **Docker** required for many integration tests (`docker-compose.yml`).
- **Style:** inline assertions, `SomeMethod().Should().Be(expected)`; prefer `[Theory]` with input data over duplicated tests.
- **Patterns:** extract interfaces for environment/filesystem deps (e.g. `IEnvironmentVariableProvider`); use struct implementations with generic constraints for zero-allocation production code, mock structs in tests (e.g. `MockEnvironmentVariableProvider`).
- **`docs/development/TracerDebugging.md`** — Local debugging, launchSettings.json, `$(SolutionDir)` path issues, IDE tips, troubleshooting tracer loading.

## Commit & Pull Request Guidelines

- Commits: imperative mood, optional `[Area]` prefix (e.g. `[Debugger]`, `[SymDB]`). Keep messages concise.
- PRs: follow [`.github/pull_request_template.md`](.github/pull_request_template.md); focus on "what" and "why".

## Configuration

- **`tracer/src/Datadog.Trace/Configuration/supported-configurations.yaml`** — Config metadata, product categorization, aliases, deprecations, and defaults for all `DD_*`/`OTEL_*` variables (also consumed by source generators).
- **`docs/development/Configuration/AddingConfigurationKeys.md`** — Adding config keys: YAML, source generators, aliases, telemetry normalization, analyzers.

## Security & Configuration

- Do not commit secrets; prefer env vars (`DD_*`). `.env` must not contain credentials.
- Use `global.json` SDK; confirm with `dotnet --version`.

## Documentation References

- `docs/README.md` — Overview. `docs/CONTRIBUTING.md` — Contribution process / external PR policy. `docs/RUNTIME_SUPPORT_POLICY.md` — Supported runtimes.
- `docs/development/UpdatingTheSdk.md` — SDK updates. `docs/development/QueryingDatadogAPIs.md` — Querying spans/logs for debugging.
- `docs/development/CI/TroubleshootingCIFailures.md` — Azure DevOps build/test failures. `docs/development/CI/RunSmokeTestsLocally.md` — Local smoke tests.

(Topic-specific guides — integrations, duck typing, Azure Functions, debugging — are linked inline in their sections above.)

## Glossary

- **AAS** — Azure App Services
- **AAP** — App and API Protection (formerly ASM/AppSec)
- **AOT** — Ahead-of-Time (compilation)
- **APM** — Application Performance Monitoring
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
