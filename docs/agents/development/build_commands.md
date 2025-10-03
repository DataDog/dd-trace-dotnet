# Build, Test, and Development Commands

## Quick Commands

- Build tracer home (default): `./tracer/build.sh` or `powershell ./tracer/build.ps1`.
- Show config: `./tracer/build.sh Info`.
- Unit tests (managed): `./tracer/build.sh BuildAndRunManagedUnitTests`.
- Unit tests (native): `./tracer/build.sh RunNativeUnitTests`.
- Integration tests: Linux `BuildAndRunLinuxIntegrationTests`, macOS `BuildAndRunOsxIntegrationTests`, Windows `BuildAndRunWindowsIntegrationTests`.
- Package artifacts: `./tracer/build.sh PackageTracerHome`.
- Coverage: append `--code-coverage-enabled true`.

## Nuke Targets

Usage: `./tracer/build.sh <Target> [--option value]` (Windows: `powershell ./tracer/build.ps1 <Target>`). List all with `--help`.

### Core Targets
- `Info` — Print current build config.
- `Clean` — Remove build outputs/obj/bin.
- `BuildTracerHome` — Build native + managed tracer and publish monitoring home (default).
- `BuildProfilerHome` — Build/provision profiler artifacts.
- `BuildNativeLoader` — Build and publish native loader.
- `PackNuGet` / `PackageTracerHome` — Create NuGet/MSI/zip artifacts.

### Tests (managed/native)
- `BuildAndRunManagedUnitTests` — Build and run managed unit tests.
- `RunNativeUnitTests` — Build and run native unit tests.
- `RunIntegrationTests` — Execute integration tests (used by OS-specific wrappers below).
- Debugger: `BuildAndRunDebuggerIntegrationTests`.

### Platform Wrappers
- Windows: `BuildAndRunWindowsIntegrationTests`, `BuildAndRunWindowsRegressionTests`, `BuildAndRunWindowsAzureFunctionsTests`.
- Linux: `BuildAndRunLinuxIntegrationTests`.
- macOS: `BuildAndRunOsxIntegrationTests`.

### Profiler & Native
- `CompileProfilerNativeSrc`, `CompileProfilerNativeTests`, `RunProfilerNativeUnitTests{Windows|Linux}`.
- Static analysis: `RunClangTidyProfiler{Windows|Linux}`, `RunCppCheckProfiler{Windows|Linux}`.
- Native loader tests: `CompileNativeLoader*`, `RunNativeLoaderTests{Windows|Linux}`.

### Tools & Utilities
- `BuildRunnerTool`, `PackRunnerToolNuget`, `BuildStandaloneTool`, `InstallDdTraceTool`.
- `RunBenchmarks` — Execute performance benchmarks.
- `UpdateSnapshots`, `PrintSnapshotsDiff` — Snapshot testing utilities.
- `UpdateVersion`, `GenerateSpanDocumentation`, `RunInstrumentationGenerator` — Maintenance.

## Windows Development

- Use forward slashes (`/`) as path separators instead of backslashes (`\`) to avoid string escaping issues in commands and scripts
  - Example: `D:/source/datadog` instead of `D:\source\datadog`
  - Applies to: Git commands, bash scripts, curl, and other CLI tools
- Build: `./tracer/build.cmd` (or `./tracer/build.cmd <Target>`)
- PowerShell: Use `pwsh` instead of `powershell` when running PowerShell scripts
- Git Bash: Forward slashes work in both Git Bash and native Windows terminals

## macOS Development

- Prereqs: Install .NET SDK, Xcode Command Line Tools, and `cmake` (`brew install cmake`).
- Build: `./tracer/build.sh Clean BuildTracerHome`.
- Unit tests: `./tracer/build.sh BuildAndRunManagedUnitTests BuildAndRunNativeUnitTests`.
- Integration tests: `docker-compose up StartDependencies.OSXARM64`; run `./tracer/build.sh BuildAndRunOsxIntegrationTests`; `docker-compose down` to stop.
- Filters as needed: `--framework net6.0` and `--filter "Category=Smoke"`.
- Apple Silicon: Some services are x86-only; see `docker-compose.yml` comments. Consider Colima if you need amd64 containers.
- Details: see `tracer/README.MD` (macOS section).
