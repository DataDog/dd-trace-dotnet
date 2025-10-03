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

See [tracer structure documentation](docs/agents/architecture/tracer_structure.md) for detailed component breakdown.

Load when you need to understand the detailed structure of the tracer codebase.

## Build, Test, and Development Commands

See [build commands documentation](docs/agents/development/build_commands.md) for comprehensive build and test commands.

Quick reference:
- Build: `./tracer/build.sh` (or `./tracer/build.cmd` on Windows)
- Tests: `./tracer/build.sh BuildAndRunManagedUnitTests`
- Windows: Use forward slashes (`/`) in paths; use `pwsh` instead of `powershell`

## Coding Style & Naming Conventions

See [coding style documentation](docs/agents/development/coding_style.md) for complete style guidelines.

Quick reference:
- C#: 4-space indent, PascalCase types/methods, camelCase locals, prefer `var`, modern collection expressions (`[]`)
- Add missing `using` directives instead of fully-qualified type names
- StyleCop: Address warnings before pushing

Load when writing or reviewing code.

## Performance Guidelines

See [performance guidelines documentation](docs/agents/development/performance_guidelines.md) for detailed performance optimization patterns.

**Critical:** Minimize allocations in bootstrap/startup code and hot paths (span creation, context propagation, sampling).

Load when working on performance-sensitive code paths.

## Testing Guidelines

See [testing guidelines documentation](docs/agents/development/testing_guidelines.md) for testing frameworks, patterns, and Datadog API verification.

Quick reference:
- Frameworks: xUnit (managed), GoogleTest (native)
- Docker services in `docker-compose.yml`
- Verify with Datadog API: spans endpoint for instrumentation, logs endpoint for diagnostics

Load when writing tests or verifying instrumentation.

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

## CallTarget Wiring

See [CallTarget wiring documentation](docs/agents/architecture/calltarget_wiring.md) for how automatic instrumentation works via CallTarget mechanism.

Load when working on integrations or understanding the auto-instrumentation pipeline.

## Duck Typing Mechanism

See [duck typing documentation](docs/agents/architecture/duck_typing.md) for duck typing implementation details.

Load when creating integrations that need to interact with external types without hard dependencies.

## Integrations

See [creating integrations documentation](docs/agents/integrations/creating_integrations.md) for step-by-step integration creation guide.

Load when adding new integrations for third-party libraries.

## Serverless Environments

### Azure App Service

See [Azure App Service documentation](docs/agents/deployment/azure_app_service.md) for Azure App Service setup and capabilities.

Load when working on Azure App Service instrumentation.

### AWS Lambda

See [AWS Lambda documentation](docs/agents/deployment/aws_lambda.md) for Lambda installation methods, layers, and environment variables.

Load when working on AWS Lambda instrumentation.

### Azure Functions

See [Azure Functions documentation](docs/agents/deployment/azure_functions.md) for Azure Functions setup across different hosting plans.

Load when working on Azure Functions instrumentation or testing.

## General .NET Tracer Installation

### .NET Core / .NET 5+

See [.NET Core installation documentation](docs/agents/deployment/installation_dotnet_core.md) for installation methods and enabling the tracer.

Load when working on .NET Core installation or troubleshooting.

### .NET Framework

See [.NET Framework installation documentation](docs/agents/deployment/installation_dotnet_framework.md) for .NET Framework-specific installation.

Load when working on .NET Framework installation or IIS/Windows Services setup.

## Agentless Logging

See [agentless logging documentation](docs/agents/deployment/agentless_logging.md) for direct log submission to Datadog.

Load when working on serverless logging or troubleshooting log delivery.

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
