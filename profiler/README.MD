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

#### Minimun requirements

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