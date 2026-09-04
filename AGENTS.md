# Repository Guidelines

This file is the coding-agent operating guide and task-routing reference for this repository. Read the linked guide for a specialized subsystem before changing it.

## Agent Workflow

For non-trivial work:

1. Identify the affected area, read its guide, and define verifiable success criteria.
2. Keep the change narrowly scoped. Mention unrelated problems, but do not fix them unless asked.
3. Never manually edit generated files (`.g.` in the extension). Read the file header and use the documented regeneration command.
4. Add a regression test for bug fixes when practical.
5. Run the smallest relevant test first, then the full affected suite for feature work when practical. Do not run every repository test by default.
6. Report the exact verification commands, pass/fail counts, skipped tests, and anything not run. Never claim a command passed unless it was executed. Explain platform, Docker, or infrastructure constraints that prevented verification.

## Repository and Architecture

- `tracer/src` — Managed tracer, analyzers, generators, and tooling.
- `tracer/test` — Managed unit and integration tests; samples are under `tracer/test/test-applications`.
- `profiler/src`, `profiler/test` — Native Continuous Profiler and tests.
- `shared` — Cross-cutting native libraries and utilities.
- `docs` — Product and developer documentation; start with `docs/README.md`.
- `docker-compose.yml` — Integration-test dependencies.
- Main IDE solutions: `Datadog.Trace.sln` and `Datadog.Profiler.sln`.

Data flow and loading:

- The native CLR profiler hooks the runtime through CallTarget and loads the managed tracer.
- `Datadog.Trace` creates spans, propagates context, samples traces, and instruments libraries.
- Build outputs publish a monitoring home; the native loader boots the tracer from that home.
- Nuke coordinates .NET builds and CMake/vcpkg native builds.

Core managed tracer areas under `tracer/src/Datadog.Trace`:

- Instrumentation: `ClrProfiler`, `DiagnosticListeners`, `DuckTyping`, and `Activity`.
- Trace pipeline: `Agent`, `Processors`, `Propagators`, `Sampling`, and `Tagging`.
- Configuration and diagnostics: `Configuration`, `Logging`, `Telemetry`, and `PlatformHelpers`.
- Products: `AppSec`, `Ci`, `ContinuousProfiler`, `Debugger`, `DataStreamsMonitoring`, and `DatabaseMonitoring`.
- Supporting components: `RuntimeMetrics`, `RemoteConfigurationManagement`, `LibDatadog`, and `HttpOverStreams`.

Other modules under `tracer/src` include the managed loader, manual instrumentation API, source generators, OpenTracing bridge, MSBuild tasks, CLI tools, trimming support, Azure Functions support, Fleet Installer, and pre-instrumented assembly tooling. `Datadog.Tracer.Native` contains native interop and packaging metadata.

### NuGet Package Architecture

The `Datadog.Trace` NuGet package ships only the manual instrumentation API, `Datadog.Trace.Manual.dll`; it does not ship auto-instrumentation code or native profiler binaries. Customer code references it for APIs such as `Tracer.Instance.StartActive()`.

The full `Datadog.Trace.dll` contains auto-instrumentation code and is delivered through the monitoring home, installers, or specialized packages such as `Datadog.Trace.Bundle` and `Datadog.AzureFunctions`. The native profiler loads this assembly from the monitoring home.

## Build and Test Commands

Run commands from the repository root. Unit and integration test targets require `BuildTracerHome` to have been built first. See `tracer/README.md` for prerequisites, Docker and dev-container workflows, filtered tests, and additional Nuke targets.

### Windows

```powershell
.\tracer\build.cmd Clean BuildTracerHome
.\tracer\build.cmd BuildAndRunManagedUnitTests BuildAndRunNativeUnitTests
.\tracer\build.cmd BuildAndRunIntegrationTests
```

### Linux (recommended Docker workflow)

```bash
./tracer/build_in_docker.sh Clean BuildTracerHome
./tracer/build_in_docker.sh BuildAndRunManagedUnitTests
./tracer/build_in_docker.sh BuildAndRunIntegrationTests
```

### macOS

```bash
./tracer/build.sh Clean BuildTracerHome
./tracer/build.sh BuildAndRunManagedUnitTests BuildAndRunNativeUnitTests
./tracer/build.sh BuildAndRunIntegrationTests
```

## Creating Integrations

- Add integrations under `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/<Area>`.
- Add `[InstrumentMethod]` with the assembly, type, method, and supported version range.
- Implement `OnMethodBegin` and `OnMethodEnd` or `OnAsyncMethodEnd` handlers.
- Use constrained duck types or `DuckCast<T>()` for third-party types.
- Add tests under `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests` and samples under `tracer/test/test-applications/integrations`.

Read `docs/development/AutomaticInstrumentation.md`, `docs/development/InstrumentationGenerator.md`, and `docs/development/DuckTyping.md` before implementing an integration. Use `docs/development/for-ai/InstrumentationGenerator-CLI.md` for the CLI schemas and error behavior.

Generate CLI boilerplate from the repository root:

```powershell
# Windows
.\tracer\build.cmd RunInstrumentationGeneratorCli --assembly-path <dll> --type-name <type> --method-name <method>
```

```bash
# Linux/macOS
./tracer/build.sh RunInstrumentationGeneratorCli --assembly-path <dll> --type-name <type> --method-name <method>
```

The GUI generator is primarily a Windows workflow:

```powershell
.\tracer\build.ps1 RunInstrumentationGenerator
```

## Specialized Task Routing

### Azure Functions and Serverless

- Use the Azure App Services Site Extension on Windows Premium, Elastic Premium, and Dedicated plans; use the `Datadog.AzureFunctions` NuGet package for Linux Consumption and Container Apps.
- Read `docs/development/AzureFunctions.md` and `docs/development/for-ai/AzureFunctions-Architecture.md` before changing Azure Functions instrumentation.
- Samples are under `tracer/test/test-applications/azure-functions`.
- Run the Windows test target with `.\tracer\build.cmd BuildAndRunWindowsAzureFunctionsTests`.
- Read `docs/development/AwsLambdaIntegrationTests.md` for AWS Lambda integration tests.
- Consult the upstream [Azure Functions Host](https://github.com/Azure/azure-functions-host) and [.NET Worker](https://github.com/Azure/azure-functions-dotnet-worker) repositories when host or worker behavior matters.

### Debugger and Dynamic Instrumentation

Debugger code inspects live customer objects. Before changing capture, expression evaluation, Exception Replay, Code Origin, or symbol resolution, read `docs/development/DebuggerSafetyBoundaries.md`. Avoid paths that load customer types early, trigger type initializers, instantiate attributes, or execute getters, enumerators, exception overrides, or `ToString()`.

For general local debugging, read `docs/development/TracerDebugging.md`. For querying spans and logs during investigations, read `docs/development/QueryingDatadogAPIs.md`.

### Configuration and SDK Maintenance

- Treat `tracer/src/Datadog.Trace/Configuration/supported-configurations.yaml` as the source of truth for `DD_*` and `OTEL_*` configuration metadata, aliases, deprecations, and defaults.
- Follow `docs/development/Configuration/AddingConfigurationKeys.md` when adding configuration keys.
- Follow `docs/development/UpdatingTheSdk.md` for SDK updates.
- Check `docs/RUNTIME_SUPPORT_POLICY.md` before making runtime-compatibility assumptions.

## Coding Standards

### C#

- Follow `.editorconfig` and `tracer/stylecop.json`; address analyzer and StyleCop warnings.
- Add `using` directives instead of fully qualified type names.
- Prefer modern C# syntax and collection expressions (`[]`), but do not use APIs or language constructs whose required types are unavailable on supported target frameworks. For example, avoid `ValueTuple` syntax in .NET Framework 4.6.1 code.
- Prefer `is not null` to `!= null`.
- In compatible tracer projects where the helper is available, use `StringUtil.IsNullOrEmpty()` for multi-target compatibility; do not assume every repository project exposes it.

### C and C++

Follow `.clang-format` and the surrounding naming conventions.

## Logging Guidelines

Use unambiguous customer-facing terms in high-level logs:

- **Datadog SDK** for the complete monitoring solution.
- **Instrumentation** or **Instrumentation component** for native tracer auto-instrumentation.
- **Continuous Profiler** for the profiling product.
- **Datadog.Trace.dll** for the managed tracer assembly; do not call it the managed profiler.

Internal technical names such as native loader, native tracer, managed tracer loader, managed tracer, Libdatadog, and `CorProfiler` remain valid in technical contexts.

Do not allocate numeric strings in log calls:

```csharp
// Bad
Log.Debug(ex, "Error (attempt {Attempt})", (attempt + 1).ToString());

// Good
Log.Debug<int>(ex, "Error (attempt {Attempt})", attempt + 1);
```

For retries, log intermediate expected failures at Debug and the final exhausted or non-retryable failure at Error.

Use `Log.ErrorSkipTelemetry` for expected environmental or transient errors, such as endpoint unavailability. Do not use it for HTTP 400 responses, bugs, or outer catch blocks that only receive unexpected exceptions after inner methods handled expected failures.

Final network-failure messages must include the endpoint, number of attempts, and a troubleshooting-documentation link.

## Performance and Testing

The tracer runs inside customer processes. Treat startup code and hot paths as performance-critical, including loader initialization, static constructors, configuration loading, integration registration, span creation, tagging, propagation, sampling, and instrumentation callbacks.

In these paths:

- Use `readonly struct` providers with generic interface constraints when they avoid boxing.
- Avoid interpolated logging and `params` array allocations; use format strings and fixed-arity overloads.
- Measure or benchmark meaningful performance-sensitive changes when practical.

Do not introduce interfaces, provider structs, or generic constraints mechanically in ordinary code. Use them where dependencies need substitution or where a demonstrated critical path benefits.

Tests use xUnit for managed code and GoogleTest for native code. Prefer inline assertions such as `SomeMethod().Should().Be(expected)` and `[Theory]` data over duplicated `[Fact]` tests. Many integration tests require Docker services from `docker-compose.yml`.

For CI failures and smoke tests, use `docs/development/CI/TroubleshootingCIFailures.md` and `docs/development/CI/RunSmokeTestsLocally.md`.

## Shell and Command-Line Safety

Use syntax for the active shell; do not mix shell dialects:

- `cmd.exe`: `>NUL` or `2>NUL`
- PowerShell: `> $null` or `2> $null`
- Bash, Git Bash, WSL, Linux, and macOS: `>/dev/null` or `2>/dev/null`

In a Unix-like shell, `>nul` can create an ordinary file instead of targeting a null device. Prefer retaining diagnostics unless suppression is necessary. When available, prefer structured file-reading and repository-search capabilities over complex shell pipelines.

## Commits, Pull Requests, and Security

- Use imperative commit messages with an optional area prefix such as `[Debugger]` or `[SymDB]`.
- Follow `docs/CONTRIBUTING.md` and `.github/pull_request_template.md`; explain what and why, and include how only when it is non-obvious.
- Follow `docs/development/GitHubActionsSecurity.md` for action allowlisting and SHA pinning.
- Do not commit secrets. Use environment variables for `DD_*` credentials, and do not put credentials in `.env`.
- Use the SDK selected by `global.json`; confirm it with `dotnet --version`.

## Datadog-Specific Glossary

- **AAP / ASM** — App and API Protection, formerly ASM/AppSec.
- **CP** — Continuous Profiler.
- **DI** — Dynamic Instrumentation.
- **DSM** — Data Streams Monitoring.
- **RASP** — Runtime Application Self-Protection.
- **RCM** — Remote Configuration Management.
