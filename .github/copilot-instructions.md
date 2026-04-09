# GitHub Copilot Instructions for dd-trace-dotnet

> This file provides guidance for GitHub Copilot cloud agents working in this repository for the first time. See `AGENTS.md` at the repository root for the full reference; this file summarises the most important points.

## Repository Overview

`dd-trace-dotnet` is the Datadog .NET APM Tracer. It provides:
- **Auto-instrumentation**: A native CLR profiler hooks the .NET runtime (via `ICorProfiler` / CallTarget) and loads the managed tracer.
- **Manual instrumentation**: `Datadog.Trace.Manual.dll` (shipped in the `Datadog.Trace` NuGet package).
- **Continuous Profiler**, **Application Security (AAP/IAST)**, **Dynamic Instrumentation**, **Data Streams Monitoring**, and more.

## Directory Layout

| Path | Contents |
|------|----------|
| `tracer/src/` | Managed tracer, analyzers, CLI tools |
| `tracer/test/` | Unit and integration tests; sample apps under `test/test-applications/` |
| `profiler/src/`, `profiler/test/` | Native Continuous Profiler |
| `shared/` | Cross-cutting native libraries/utilities |
| `docs/` | Product and developer documentation |
| `docker-compose.yml` | Test dependencies (databases, brokers, …) |
| `Datadog.Trace.sln` | Main solution (IDE entry point) |

## Build System

- **Build orchestration**: [Nuke](https://nuke.build/) — entry points are `tracer/build.sh` (Linux/macOS) or `tracer\build.cmd` (Windows).
- **SDK**: Pinned to .NET SDK `10.0.100` via `global.json` (`rollForward: minor`).
- **C# language version**: `latest` (set in `tracer/Directory.Build.props`).
- **Warnings as errors**: `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` is set globally.
- **Native components**: CMake + vcpkg (see `CMakeLists.txt`, `vcpkg.json`).

### Key build targets

```bash
# Build the tracer home (managed + native outputs)
./tracer/build.sh BuildTracerHome

# Managed unit tests
./tracer/build.sh BuildAndRunManagedUnitTests

# Native unit tests
./tracer/build.sh BuildAndRunNativeUnitTests

# Integration tests (platform-specific)
./tracer/build.sh BuildAndRunLinuxIntegrationTests
./tracer/build.sh BuildAndRunWindowsIntegrationTests
./tracer/build.sh BuildAndRunOsxIntegrationTests
```

See `tracer/README.md` for full setup instructions, Visual Studio requirements, and all available Nuke targets.

## Core Managed Tracer (`tracer/src/Datadog.Trace`)

Key sub-directories:

| Sub-dir | Purpose |
|---------|---------|
| `ClrProfiler/AutoInstrumentation/` | All library integrations, grouped by technology (AdoNet, AspNetCore, Kafka, Redis, …) |
| `ClrProfiler/CallTarget/` | CallTarget invoker, handlers, state structs, async continuations |
| `Configuration/` | Settings, environment parsing, `TracerSettings`, source-generated keys |
| `Agent/` | Transport, MessagePack encoding, health checks |
| `Propagators/` | Datadog, W3C, B3 context propagation |
| `Sampling/` | Samplers, priority decisions |
| `Tagging/` | Strongly-typed tag sets |
| `Telemetry/` | Product telemetry emission |
| `DuckTyping/` | Version-agnostic duck-typing runtime |
| `Generated/` | **Do not edit manually** — see file headers for regeneration commands |

## Creating a New Auto-instrumentation Integration

1. Add a new `.cs` file under `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/<Area>/`.
2. Add `[InstrumentMethod]` attribute specifying assembly, type, method, and version range.
3. Implement `OnMethodBegin` and `OnMethodEnd` / `OnAsyncMethodEnd`.
4. Use duck-typing for third-party types (`where TTarget : IMyShape, IDuckType` or `obj.DuckCast<IMyShape>()`).
5. Add integration tests under `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/` with sample apps in `tracer/test/test-applications/integrations/`.
6. Generate boilerplate: `./tracer/build.ps1 RunInstrumentationGenerator`.

Full guide: `docs/development/AutomaticInstrumentation.md`  
Duck typing: `docs/development/DuckTyping.md`

## Adding Configuration Keys

Configuration keys are **source-generated** from a single YAML file:

- **Definition**: `tracer/src/Datadog.Trace/Configuration/supported-configurations.yaml`
- **Never edit** `*.g.cs` files manually.
- After editing the YAML, rebuild to regenerate `ConfigurationKeys.g.cs` and `ConfigurationKeyMatcher.g.cs`.

Full guide: `docs/development/Configuration/AddingConfigurationKeys.md`

## Coding Standards

### C# Style
- 4-space indent; `System.*` using directives first; prefer `var`.
- Types and methods: `PascalCase`; locals: `camelCase`.
- Add `using` directives rather than fully-qualifying type names.
- Use modern C# syntax (`is not null`, collection expressions `[]`), **but** avoid features unavailable on .NET Framework 4.6.1 (e.g. no `ValueTuple` syntax).
- Use `StringUtil.IsNullOrEmpty()` instead of `string.IsNullOrEmpty()` for cross-runtime compatibility.
- StyleCop is enforced — resolve warnings before pushing (`tracer/stylecop.json`).

### Generated Files
Files with `.g.` in their name are auto-generated. **Never edit them directly.** The file header contains the regeneration command.

### Logging
- Do **not** use `ToString()` on numeric types in log calls — use generic overloads: `Log.Debug<int>("Value: {V}", x)`.
- Use `Log.ErrorSkipTelemetry` only for expected environmental/transient errors (network outages, endpoint unavailability). Use `Log.Error` for bugs or unexpected failures.
- Include endpoint, attempt count, and a docs link in final failure messages for network operations.

### Performance
The tracer runs in-process with customer applications:
- Use `readonly struct` providers with generic constraints to avoid boxing (zero-allocation pattern).
- Prefer format-string logging over string interpolation to avoid unnecessary allocations.
- Provide per-arity overloads to avoid `params` array allocations on hot paths.

### Terminology in Logs / Comments
| Avoid | Use instead |
|-------|-------------|
| "profiler" (ambiguous) | "Instrumentation", "Continuous Profiler", "Datadog SDK" |
| "managed profiler" | "Datadog.Trace.dll" |

## Testing

- **Framework**: xUnit for managed code; GoogleTest for native.
- **Assertion style**: `result.Should().Be(expected)` (FluentAssertions inline).
- **Integration tests**: Require Docker; services defined in `docker-compose.yml`.
- **Test filters**: `--filter "Category=Smoke"` or `--framework net6.0`.
- Use `[Theory]` with inline data rather than duplicating test methods.
- Interfaces + struct implementations enable zero-allocation production code while mock classes satisfy tests (see `MockEnvironmentVariableProvider` pattern).

## Pull Request Guidelines

Follow `.github/pull_request_template.md`:

```
## Summary of changes
## Reason for change
## Implementation details
## Test coverage
## Other details
```

- Commits: imperative mood, optional scope tag (`fix(telemetry): …`), reference issues.
- CI: All checks must pass. The primary pipeline is **Azure DevOps**; GitHub Actions handles CodeQL, nullability checks, and snapshot verification.
- Prefer 2 approvals before merging (see PR template for details).

## CI Architecture

| System | Role |
|--------|------|
| Azure DevOps | Main build/test pipeline (Windows, Linux, macOS) |
| GitHub Actions | CodeQL, nullability checks, source generators, snapshots |
| GitLab | Additional pipeline tasks |

Investigate failures in Azure DevOps first. Build URL pattern:  
`https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/results?buildId=<BUILD_ID>`

Troubleshooting guide: `docs/development/CI/TroubleshootingCIFailures.md`

## Windows Command-Line Gotcha

**Never** redirect to `nul` on Windows (`command 2>nul`). It can create a literal file named `nul` that is nearly impossible to delete. Use `2>\\.\NUL` if suppression is essential, or simply let errors show.

## Key Documentation Files

| File | When to load |
|------|--------------|
| `AGENTS.md` | Full AI-agent reference; always useful |
| `tracer/README.md` | Dev setup, build targets, VS requirements |
| `docs/development/AutomaticInstrumentation.md` | Creating integrations |
| `docs/development/DuckTyping.md` | Working with third-party types |
| `docs/development/Configuration/AddingConfigurationKeys.md` | Adding `DD_*` config keys |
| `docs/development/TracerDebugging.md` | Local debugging and IDE config |
| `docs/development/AzureFunctions.md` | Azure Functions instrumentation |
| `docs/development/for-ai/AzureFunctions-Architecture.md` | Azure Functions architecture deep dive |
| `docs/development/CI/TroubleshootingCIFailures.md` | CI failure investigation |
| `docs/development/CI/RunSmokeTestsLocally.md` | Running smoke tests locally |
| `tracer/src/Datadog.Trace/Configuration/supported-configurations.yaml` | All `DD_*` / `OTEL_*` env vars |

## Glossary

| Acronym | Meaning |
|---------|---------|
| AAS | Azure App Services |
| AAP | App and API Protection (formerly ASM/AppSec) |
| AOT | Ahead-of-Time compilation |
| APM | Application Performance Monitoring |
| ASM | Application Security Management (now AAP) |
| CI | Continuous Integration / CI Visibility |
| CP | Continuous Profiler |
| DBM | Database Monitoring |
| DI | Dynamic Instrumentation |
| DSM | Data Streams Monitoring |
| IAST | Interactive Application Security Testing |
| JIT | Just-in-Time compiler |
| OTEL | OpenTelemetry |
| R2R | ReadyToRun |
| RASP | Runtime Application Self-Protection |
| RCM | Remote Configuration Management |
| RID | Runtime Identifier |
| TFM | Target Framework Moniker |
| WAF | Web Application Firewall |
