# Nuke Build Targets Graph

Generated: 2026-02-11

## Target Hierarchy

### Info
**Description**: Describes the current configuration
**Dependencies**: None

---

### Clean
**Description**: Cleans all build output
**Dependencies**: None

---

### BuildTracerHome (default)
**Description**: Builds the native and managed src, and publishes the tracer home directory
**Dependencies**:
- CompileManagedLoader
- BuildNativeTracerHome
- BuildManagedTracerHome
- BuildNativeLoader

---

### BuildProfilerHome
**Description**: Builds the Profiler native and managed src, and publishes the profiler home directory
**Dependencies**:
- CompileProfilerNativeSrc
- PublishProfiler

---

### BuildNativeLoader
**Description**: Builds the Native Loader, and publishes to the monitoring home directory
**Dependencies**:
- CompileNativeLoader
- PublishNativeLoader

---

### BuildNativeWrapper
**Description**: Builds the Native Wrapper
**Dependencies**:
- CompileNativeWrapper
- TestNativeWrapper
- PublishNativeWrapper

---

### PackageTracerHome
**Description**: Builds NuGet packages, MSIs, and zip files, from already built source
**Dependencies**:
- CreateRequiredDirectories
- ZipMonitoringHome
- BuildMsi
- PackNuGet

---

### BuildManagedUnitTests
**Description**: Builds the managed unit tests
**Dependencies**:
- CreateRequiredDirectories
- BuildRunnerTool
- CompileManagedUnitTests

---

### BuildAndRunManagedUnitTests
**Description**: Builds the managed unit tests and runs them
**Dependencies**:
- BuildManagedUnitTests
- RunManagedUnitTests

---

### RunNativeUnitTests
**Description**: Builds the native unit tests and runs them
**Dependencies**:
- CreateRequiredDirectories
- RunNativeTests

---

### BuildAndRunWindowsIntegrationTests
**Description**: Builds and runs the Windows (non-IIS) integration tests
**Dependencies**:
- BuildWindowsIntegrationTests
- CompileSamples
- CompileTrimmingSamples
- RunIntegrationTests

---

### BuildAndRunWindowsRegressionTests
**Description**: Builds and runs the Windows regression tests
**Dependencies**:
- BuildWindowsRegressionTests
- CompileSamples
- RunWindowsRegressionTests

---

### BuildAndRunWindowsAzureFunctionsTests
**Description**: Builds and runs the Windows Azure Functions tests
**Dependencies**:
- CompileManagedTestHelpers
- CompileAzureFunctionsSamplesWindows
- BuildRunnerTool
- CompileIntegrationTests
- RunWindowsAzureFunctionsTests

---

### BuildLinuxIntegrationTests
**Description**: Builds the linux integration tests
**Dependencies**:
- CompileManagedTestHelpers
- CompileLinuxOrOsxIntegrationTests
- CompileLinuxDdDotnetIntegrationTests
- BuildRunnerTool
- CopyNativeFilesForTests
- CopyServerlessArtifacts

---

### BuildAndRunLinuxIntegrationTests
**Description**: Builds and runs the linux integration tests. Requires docker-compose dependencies
**Dependencies**:
- BuildLinuxIntegrationTests
- RunIntegrationTests
- RunLinuxDdDotnetIntegrationTests

---

### BuildOsxIntegrationTests
**Description**: Builds the osx integration tests
**Dependencies**:
- CompileManagedTestHelpers
- CompileLinuxOrOsxIntegrationTests
- BuildRunnerTool
- CopyNativeFilesForTests
- CopyServerlessArtifacts

---

### BuildAndRunOsxIntegrationTests
**Description**: Builds and runs the osx integration tests. Requires docker-compose dependencies
**Dependencies**:
- BuildOsxIntegrationTests
- CompileSamples
- CompileTrimmingSamples
- RunIntegrationTests

---

### BuildAndRunToolArtifactTests
**Description**: Builds and runs the tool artifacts tests
**Dependencies**:
- CompileManagedTestHelpers
- InstallDdTraceTool
- BuildToolArtifactTests
- RunToolArtifactTests

---

### BuildAndRunDdDotnetArtifactTests
**Description**: Builds and runs the tool artifacts tests
**Dependencies**:
- CompileManagedTestHelpers
- BuildDdDotnetArtifactTests
- CopyDdDotnet
- RunDdDotnetArtifactTests

---

### PackNuGet
**Description**: Creates the NuGet packages from the compiled src directory
**Dependencies**:
- CreateRequiredDirectories
- CreateTrimmingFile

---

### RunBenchmarks
**Description**: Runs the Benchmarks project
**Dependencies**: None

---

### SetUpExplorationTests
**Description**: Setup exploration tests
**Dependencies**: None

---

### RunExplorationTests
**Description**: Run exploration tests
**Dependencies**:
- CleanTestLogs

---

### BuildAndRunProfilerCpuLimitTests
**Description**: Run the profiler container tests
**Dependencies**: None

---

### BuildAndRunProfilerIntegrationTests
**Description**: Builds and runs the profiler integration tests
**Dependencies**: None

---

### CopyDdDotnet
**Description**: Copies dd-dotnet artifacts
**Dependencies**: None

---

### ZipMonitoringHome
**Description**: Creates zip files for monitoring home
**Dependencies**:
- ZipMonitoringHomeWindows
- ZipMonitoringHomeLinux
- ZipMonitoringHomeOsx

---

### RunWindowsTracerIisIntegrationTests
**Description**: Runs Windows IIS integration tests
**Dependencies**: None

---

### RunWindowsMsiIntegrationTests
**Description**: Runs Windows MSI integration tests
**Dependencies**: None

---

### RunLinuxDdDotnetIntegrationTests
**Description**: Runs the linux dd-dotnet integration tests
**Dependencies**:
- CleanTestLogs

---

### InstallDdTraceTool
**Description**: Installs dd-trace tool
**Dependencies**: None

---

### CreateTrimmingFile
**Description**: Creates the Trimming.xml file
**Dependencies**: None

---

### BuildAndRunDebuggerIntegrationTests
**Description**: Builds and runs the debugger integration tests
**Dependencies**:
- BuildDebuggerIntegrationTests
- RunDebuggerIntegrationTests

---

### GacAdd
**Description**: Adds the (already built) files to the Windows GAC **REQUIRES ELEVATED PERMISSIONS**
**Dependencies**:
- GacRemove

---

### GacRemove
**Description**: Removes the Datadog tracer files from the Windows GAC **REQUIRES ELEVATED PERMISSIONS**
**Dependencies**: None

---

### RunInstrumentationGenerator
**Description**: Runs the AutoInstrumentation Generator
**Dependencies**: None

---

### BuildIisSampleApp
**Description**: Rebuilds an IIS sample app
**Dependencies**: None

---

### RunIisSample
**Description**: Runs an IIS sample app, enabling profiling
**Dependencies**: None

---

### RunDotNetSample
**Description**: Builds and runs a sample app using dotnet run, enabling profiling
**Dependencies**: None

---

### GeneratePackageVersions
**Description**: Regenerate the PackageVersions props and .cs files
**Dependencies**:
- Clean
- Restore
- CreateRequiredDirectories
- CompileManagedSrc
- PublishManagedTracer

---

### GenerateSpanDocumentation
**Description**: Regenerate documentation from our code models
**Dependencies**: None

---

### UpdateVendoredCode
**Description**: Updates the vendored dependency code and dependabot template
**Dependencies**: None

---

### UpdateVersion
**Description**: Update the version number for the tracer
**Dependencies**: None

---

### AnalyzePipelineCriticalPath
**Description**: Perform critical path analysis on the consolidated pipeline stages
**Dependencies**: None

---

### UpdateSnapshots
**Description**: Updates verified snapshots files with received ones
**Dependencies**: None

---

### PrintSnapshotsDiff
**Description**: Prints snapshots differences from the current tests
**Dependencies**: None

---

### UpdateSnapshotsFromBuild
**Description**: Updates verified snapshots downloading them from the CI given a build id
**Dependencies**: None

---

### RegenerateSolutions
**Description**: Regenerates the 'build' solutions based on the 'master' solution
**Dependencies**: None

---

### DownloadBundleNugetFromBuild
**Description**: Downloads Datadog.Trace.Bundle package from Azure DevOps and extracts it to the local bundle home directory. Useful for building Datadog.Trace.Bundle or Datadog.AzureFunctions nupkg packages locally.
**Dependencies**: None

---

## Internal/Unlisted Targets

These targets are typically used as dependencies by other targets:

### CompileManagedLoader
**Description**: Compiles the managed loader (which is required by the native loader)
**Project**: Datadog.Trace.ClrProfiler.Managed.Loader
**Output**: Datadog.Trace.ClrProfiler.Managed.Loader.dll

### CompileManagedSrc
**Description**: Compiles the managed source code
**Excludes**: Datadog.Trace.Tools.*, DataDogThreadTest, instrumented assembly projects, Datadog.AutoInstrumentation.Generator, Datadog.Trace.ClrProfiler.Managed.Loader

### BuildNativeTracerHome
**Description**: Builds the native tracer components
**Dependencies**:
- CompileTracerNativeSrc
- PublishNativeTracer
- CopyLibDatadog
- CopyLibDdwaf

### BuildManagedTracerHome
**Description**: Builds and publishes the managed tracer
**Dependencies**:
- CompileManagedSrc
- PublishManagedTracer

### CompileTracerNativeSrc
**Description**: Compiles the native tracer source (platform-specific)
**Variants**:
- CompileTracerNativeSrcWindows
- CompileTracerNativeSrcLinux
- CompileNativeSrcMacOs

### CompileNativeLoader
**Description**: Compiles the native loader (platform-specific)
**Variants**:
- CompileNativeLoaderWindows
- CompileNativeLoaderLinux
- CompileNativeLoaderOsx

### PublishNativeLoader
**Description**: Publishes the native loader to monitoring home (platform-specific)
**Variants**:
- PublishNativeLoaderWindows
- PublishNativeLoaderUnix
- PublishNativeLoaderOsx

### PublishNativeTracer
**Description**: Publishes the native tracer to monitoring home (platform-specific)
**Variants**:
- PublishNativeTracerWindows
- PublishNativeTracerUnix
- PublishNativeTracerOsx

### PublishManagedTracer
**Description**: Publishes the managed tracer assemblies to monitoring home
**Frameworks**: net462, netcoreapp3.1, net6.0, net8.0

### CompileManagedTestHelpers
**Description**: Compiles test helper libraries

### CompileManagedUnitTests
**Description**: Compiles managed unit test projects

### RunManagedUnitTests
**Description**: Runs the managed unit tests

### CompileIntegrationTests
**Description**: Compiles integration test projects

### CompileSamples
**Description**: Compiles sample applications used in tests

### CompileTrimmingSamples
**Description**: Compiles trimming-related samples

### RunIntegrationTests
**Description**: Executes integration tests

### BuildRunnerTool
**Description**: Builds the dd-trace runner tool

### CopyNativeFilesForTests
**Description**: Copies native binaries needed for tests

### CopyServerlessArtifacts
**Description**: Copies serverless-related artifacts for tests

### CreateRequiredDirectories
**Description**: Creates necessary build output directories

### Restore
**Description**: Restores NuGet packages for the solution

### CleanTestLogs
**Description**: Cleans test log directories

### DownloadLibDatadog
**Description**: Downloads libdatadog native library

### CopyLibDatadog
**Description**: Copies libdatadog to monitoring home

### DownloadLibDdwaf
**Description**: Downloads libddwaf (WAF library)

### CopyLibDdwaf
**Description**: Copies libddwaf to monitoring home

### BuildMsi
**Description**: Builds the Windows MSI installer

### CompileProfilerNativeSrc
**Description**: Compiles the Continuous Profiler native source

### PublishProfiler
**Description**: Publishes the profiler to monitoring home

### ZipMonitoringHomeWindows
**Description**: Creates Windows monitoring home zip

### ZipMonitoringHomeLinux
**Description**: Creates Linux monitoring home zip

### ZipMonitoringHomeOsx
**Description**: Creates macOS monitoring home zip

---

## Quick Reference - Common Build Scenarios

### Full Build
```bash
./tracer/build.sh BuildTracerHome
```

### Build Managed Loader Only
```bash
./tracer/build.sh CompileManagedLoader
```

### Build and Run Unit Tests
```bash
./tracer/build.sh BuildAndRunManagedUnitTests
```

### Build and Run Integration Tests (Platform-Specific)
```bash
# Windows
./tracer/build.sh BuildAndRunWindowsIntegrationTests

# Linux
./tracer/build.sh BuildAndRunLinuxIntegrationTests

# macOS
./tracer/build.sh BuildAndRunOsxIntegrationTests
```

### Package Everything
```bash
./tracer/build.sh PackageTracerHome
```

### Azure Functions Tests
```bash
./tracer/build.sh BuildAndRunWindowsAzureFunctionsTests
```

---

## Dependency Graph - Key Targets

```
BuildTracerHome (default)
├── CompileManagedLoader
│   └── Datadog.Trace.ClrProfiler.Managed.Loader.dll
├── BuildNativeTracerHome
│   ├── CompileTracerNativeSrc
│   ├── PublishNativeTracer
│   ├── CopyLibDatadog
│   └── CopyLibDdwaf
├── BuildManagedTracerHome
│   ├── CompileManagedSrc
│   └── PublishManagedTracer
└── BuildNativeLoader
    ├── CompileNativeLoader
    └── PublishNativeLoader

PackageTracerHome
├── CreateRequiredDirectories
├── ZipMonitoringHome
│   ├── ZipMonitoringHomeWindows
│   ├── ZipMonitoringHomeLinux
│   └── ZipMonitoringHomeOsx
├── BuildMsi
└── PackNuGet
    ├── CreateRequiredDirectories
    └── CreateTrimmingFile

BuildAndRunManagedUnitTests
├── BuildManagedUnitTests
│   ├── CreateRequiredDirectories
│   ├── BuildRunnerTool
│   └── CompileManagedUnitTests
└── RunManagedUnitTests

BuildAndRunWindowsIntegrationTests
├── BuildWindowsIntegrationTests
├── CompileSamples
├── CompileTrimmingSamples
└── RunIntegrationTests
```
