# dd-trace-dotnet/profiler

This folder contains the source code for the Datadog .NET APM Profiler. The .NET Profiler runs in your application process to collect managed threads call stacks related to wall time, CPU consumption, exceptions, lock contention and allocations. Read the following posts explaining implementation details:

- [Architecture and interations with the .NET CLR](https://www.datadoghq.com/blog/engineering/dotnet-continuous-profiler/)
- [CPU and walltime profiling](https://www.datadoghq.com/blog/engineering/dotnet-continuous-profiler-part-2/)
- [Exception and lock contention](https://www.datadoghq.com/blog/engineering/dotnet-continuous-profiler-part-3/)
- [Memory usage profiling](https://www.datadoghq.com/blog/engineering/dotnet-continuous-profiler-part-4/)

## Installation and usage

### Getting started

Configure the Datadog Agent for APM [as described in our documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core#configure-the-datadog-agent-for-apm). To install and enable the .NET Profiler follow [the steps as described in our documentation](https://docs.datadoghq.com/profiler/enabling/dotnet).

## Development

You can develop the profiler on various environments.

### Windows

#### Setup and minimum requirements
- [Visual Studio 2022](https://visualstudio.microsoft.com/downloads/)
  - Workloads
    - Desktop development with C++
    - .NET desktop development
    - .NET Core cross-platform development
  - Individual components
    - .NET Framework 4.7 targeting pack
    - C++ for Linux Development
    - Windows 10 SDK (10.0.18362)
    - vcpkg manager

It is needed to setup vcpkg by following the first steps of [the documentation](https://learn.microsoft.com/en-us/vcpkg/get_started/get-started-msbuild):

1. Clone the vcpkg repository
   ```cmd
   git clone https://github.com/microsoft/vcpkg.git
   ```

2. Run the bootstrap script

   Now that you have cloned the vcpkg repository, navigate to the vcpkg directory and execute the bootstrap script:

   ```cmd
   cd vcpkg; .\bootstrap-vcpkg.bat
   ```


3. Integrate with Visual Studio MSBuild

   Run the following command:
   ```cmd
   .\vcpkg.exe integrate install
   ```


#### Building in Visual Studio

Open the solution `dd-trace-dotnet\Datadog.Profiler.sln` and build the projects `Datadog.Profiler.Native.Windows` (C++).

#### Building with script
Go to the Tracer folder and use the NUKE build.cmd script

```cmd
..\tracer\build.cmd BuildProfilerHome BuildNativeLoader
```

Note: build Release binaries by default; use `--buildConfiguration Debug` for debug build

#### Debugging with Visual Studio
In the generation solution `dd-trace-dotnet\Datadog.Profiler.sln`, look at the C# projects under the Demos folder for specific scenarios:
- **Samples.BuggyBits**: web app updated from Tess Ferrandez [repository](https://github.com/TessFerrandez/BuggyBits) that implements anti-patterns suc has too many allocations/exceptions, memory leak or outgoing HTTP requests  
- **Samples.Computer01**: contains simple scenarios for end to end tests in worse case situations

In both cases, the `Program.cs` file contains the list of scenarios. When you want to debug a list of scenarios, OR the corresponding enumeration values and add it after `"CommandLineArgs": --scenario` it in the `Properties\LaunchSettings.json` file.

As a final step, enable the profiler you would like to debug by setting the corresponding `DD_PROFILING_xxx_ENABLED=1`

Note:
- the .pprof files are generated in the folder given by the `DD_INTERNAL_PROFILING_OUTPUT_DIR` environment variable
- the log files are stored in the `C:\ProgramData\Datadog .NET Tracer\logs` folder

### Linux

#### Build and run a scenario

Run these commands from the repository root. You need Docker on Linux or macOS; see the [general Docker build instructions](../tracer/README.md#linux). No Datadog Agent is needed for local captures. On a Linux host with the [native build prerequisites](#minimum-requirements) installed, use `./tracer/build.sh` instead.

##### 1. Build once

```bash
./tracer/build_in_docker.sh BuildTracerHome BuildNativeWrapper BuildProfilerHome BuildProfilerSamples --sample-name Samples.Computer01 --framework net11.0
```

This builds the profiler, its loader and dependencies, and the sample application. If host port 5003 is busy, run `export BUILD_DOCKER_PORT=0` before using the wrapper.

##### 2. Choose a scenario

`RunProfilerScenario` supports `Samples.Computer01` only. Read its [scenario IDs and descriptions](src/Demos/Samples.Computer01/Program.cs). The default is scenario 4 (PiComputation), .NET 11, for 30 seconds. Pass the ID and sample options together, for example `--scenario "5 --timeout 10 --threads 2"` for Fibonacci.

##### 3. Edit, rebuild, run

After changing profiler C++ code:

```bash
./tracer/build_in_docker.sh BuildProfilerHome RunProfilerScenario --scenario "5 --timeout 10"
```

For other changes, replace `BuildProfilerHome` with the corresponding target:

| Changed code | Rebuild target |
| --- | --- |
| Profiler C++ | `BuildProfilerHome` |
| Native loader (`shared/src/Datadog.Trace.ClrProfiler.Native/`) | `BuildNativeLoader` |
| Linux API wrapper (`profiler/src/ProfilerEngine/Datadog.Linux.ApiWrapper/`) | `BuildNativeWrapper` |
| Managed or native tracer | `BuildTracerHome` |
| Sample application | `BuildProfilerSamples --sample-name Samples.Computer01 --framework net11.0` |

To rerun without rebuilding, omit `BuildProfilerHome`. Use the same `--framework` and `--build-configuration` when building and running. Append `--extra-env-vars DD_PROFILING_ALLOCATION_ENABLED=1 DD_TRACE_DEBUG=1` for allocation profiling and debug logs.

#### What to expect

A successful run prints `Produced N profile(s)`. Logs are under `artifacts/build_data/logs/profiler_scenario/Samples.Computer01/`, with captures in its `pprof/` subdirectory. Each run replaces those outputs.

The command fails if the application fails or no nonempty profile/native profiler log is produced. For attach failures, check the native loader log first, then `DD-DotNet-Profiler-Native-*.log`.

#### Inspect a capture

For a Zstandard-compressed capture (requires `zstd` and Go):

```bash
zstd -dc path/to/capture.pprof > /tmp/profile.pb
go tool pprof -top -symbolize=none -sample_index=cpu /tmp/profile.pb
```

For an uncompressed capture, pass it directly to `pprof`. Look for your scenario's frames in the output.

#### Run sanitizer checks

On Linux with the ASAN toolchain/runtime and .NET 7 runtime installed ([CI setup](../.azure-pipelines/ultimate-pipeline.yml)):

```bash
./tracer/build.sh RunProfilerAsanTest --framework net7.0
```

This builds with ASAN, runs native tests, and runs scenario 1 for 120 seconds. Results are under `profiler/build_data/`. Do not use the Alpine Docker wrapper for this target.

**Warning:** normal, ASAN, and UBSAN builds share the CMake cache and published binaries, so they can interfere. Run `Clean` and rebuild when switching modes; this deletes build outputs and loses the incremental cache. Build isolation is tracked in [PROF-16371](https://datadoghq.atlassian.net/browse/PROF-16371).

#### Minimum requirements

To build C# projects
- [Install the latest .NET SDK](https://docs.microsoft.com/en-us/dotnet/core/install/linux)

To build C++ projects
- Clang >= 9.0 (recommended)
- CMake >= 3.14
- Libtool
- liblzma
- libssl-dev
- autoconf 
- git

#### Building from the command line
Go to the Tracer folder and use the NUKE build.cmd script

```bash
../tracer/build.sh BuildProfilerHome BuildNativeLoader
```

You could also use the following to Build C++ projects and run the unit tests
```bash
CXX=clang++ CC=clang cmake -S dd-trace-dotnet -B _build
cd _build
make -j
ctest
```

Note: the clang compiler often finds errors that are not detected by Visual Studio