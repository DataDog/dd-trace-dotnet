# dd-trace-dotnet/profiler

This folder contains the source code for the Datadog .NET APM Profiler. The .NET Profiler runs in your application process and collects managed threads callstacks.

## Installation and usage

### Getting started

Configure the Datadog Agent for APM [as described in our documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core#configure-the-datadog-agent-for-apm). To install and enable the .NET Profiler follow [the steps as described in our documentation](https://docs.datadoghq.com/tracing/profiler/enabling/dotnet/?tab=internetinformationservicesiis#installation).

## Development

You can develop the profiler on various environments.

### Windows

#### Setup and minimum requirements
- [Visual Studio 2019](https://visualstudio.microsoft.com/downloads/)
  - Workloads
    - Desktop development with C++
    - .NET desktop development
    - .NET Core cross-platform development
  - Individual components
    - .NET Framework 4.7 targeting pack
    - C++ for Linux Development
    - Windows 10 SDK (10.0.18362)

#### Building in Visual Studio

In order to build the profiler library, you need first to build the `Managed Loader` :

Open the solution `dd-trace-dotnet\shared\src\managed-lib\ManagedLoader\ManagedLoader.sln` and build the `Datadog.AutoInstrumentation.ManagedLoader` project.

Now, you can open the solution `dd-trace-dotnet\profiler\src\ProfilerEngine\Datadog.AutoInstrumentation.Profiler.sln` and build the projects `Datadog.Profiler.Managed` (C#) and `Datadog.Profiler.Native.Windows` (C++).

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
1. Build C# projects

- Managed loader
```bash
cd dd-trace-dotnet/shared/src/managed-lib/ManagedLoader/Datadog.AutoInstrumentation.ManagedLoader
dotnet build Datadog.AutoInstrumentation.ManagedLoader.csproj
```

- Managed Profiler
```bash
cd dd-trace-dotnet/profiler/src/ProfilerEngine/Datadog.Profiler.Managed
dotnet build Datadog.Profiler.Managed.csproj
```

2. Build C++ projects and run the unit tests
```bash
CXX=clang++ CC=clang cmake -S dd-trace-dotnet -B _build
cd _build
make
ctest
```
