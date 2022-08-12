# dd-trace-dotnet/profiler

This folder contains the source code for the Datadog .NET APM Profiler. The .NET Profiler runs in your application process to collect managed threads callstacks related to wall time, CPU consumption, and exceptions.

## Installation and usage

### Getting started

Configure the Datadog Agent for APM [as described in our documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core#configure-the-datadog-agent-for-apm). To install and enable the .NET Profiler follow [the steps as described in our documentation](https://docs.datadoghq.com/profiler/enabling/dotnet).

## Development

You can develop the profiler on various environments.

### Windows

#### Setup and minimum requirements
- [Visual Studio 2019/2022](https://visualstudio.microsoft.com/downloads/)
  - Workloads
    - Desktop development with C++
    - .NET desktop development
    - .NET Core cross-platform development
  - Individual components
    - .NET Framework 4.7 targeting pack
    - C++ for Linux Development
    - Windows 10 SDK (10.0.18362)

#### Building in Visual Studio

Open the solution `dd-trace-dotnet\Datadog.Profiler.sln` and build the projects `Datadog.Profiler.Native.Windows` (C++).

### Linux

#### Minimun requirements

To build C# projects
- [Install .NET 5.0 SDK](https://docs.microsoft.com/en-us/dotnet/core/install/linux)

To build C++ projects
- Clang >= 9.0 (recommended)
- CMake >= 3.14
- Libtool
- liblzma
- libssl-dev
- autoconf 
- git

#### Building from the command line
Build C++ projects and run the unit tests
```bash
CXX=clang++ CC=clang cmake -S dd-trace-dotnet -B _build
cd _build
make
ctest
```
