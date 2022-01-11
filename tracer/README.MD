# dd-trace-dotnet/tracer

This folder contains the source code for the Datadog .NET APM Tracer. The .NET Tracer automatically instruments supported libraries out-of-the-box and also supports custom instrumentation to instrument your own code.

## Installation and usage

### Getting started

Configure the Datadog Agent for APM [as described in our documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core#configure-the-datadog-agent-for-apm). For automatic instrumentation, install and enable the .NET Tracer [as described in our documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core/?tab=windows#install-the-tracer).

### Custom instrumentation

The Datadog .NET APM Tracer allows you to manually instrument your application (in addition to automatic instrumentation). To use it, please follow [the NuGet package documentation](https://github.com/DataDog/dd-trace-dotnet/tree/master/docs/Datadog.Trace/README.md)

## Development

You can develop the tracer on various environments.

### Windows

#### Minimum requirements

- [Visual Studio 2022 (v17)](https://visualstudio.microsoft.com/downloads/) or newer
  - Workloads
    - Desktop development with C++
    - .NET desktop development
    - Optional: ASP.NET and web development (to build samples)
  - Individual components
    - .NET Framework 4.7 targeting pack
- [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
  - Optional: [.NET 6.0 x86 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) to run 32-bit tests locally
- Optional: ASP.NET Core Runtimes to run tests locally
  - [ASP.NET Core 2.1](https://dotnet.microsoft.com/download/dotnet/2.1)
  - [ASP.NET Core 3.0](https://dotnet.microsoft.com/download/dotnet/3.0)
  - [ASP.NET Core 3.1](https://dotnet.microsoft.com/download/dotnet/3.1)
  - [ASP.NET Core 5.0](https://dotnet.microsoft.com/download/dotnet/5.0)
- Optional: [nuget.exe CLI](https://www.nuget.org/downloads) v5.3 or newer
- Optional: [WiX Toolset 3.11.1](http://wixtoolset.org/releases/) or newer to build Windows installer (msi)
  - [WiX Toolset Visual Studio Extension](https://wixtoolset.org/releases/) to build installer from Visual Studio
- Optional: [Docker for Windows](https://docs.docker.com/docker-for-windows/) to build Linux binaries and run integration tests on Linux containers. See [section on Docker Compose](#building-and-running-tests-with-docker-compose).
  - Requires Windows 10 (1607 Anniversary Update, Build 14393 or newer)

Microsoft provides [evaluation developer VMs](https://developer.microsoft.com/en-us/windows/downloads/virtual-machines) with Windows and Visual Studio pre-installed.

#### Building from a command line

This repository uses [Nuke](https://nuke.build/) for build automation. To see a list of possible targets run:

```cmd
.\build.cmd --help
```

For example:

```powershell
# Clean and build the main tracer project
.\build.cmd Clean BuildTracerHome

# Build and run managed and native unit tests. Requires BuildTracerHome to have previously been run
.\build.cmd BuildAndRunManagedUnitTests BuildAndRunNativeUnitTests

# Build NuGet packages and MSIs. Requires BuildTracerHome to have previously been run
.\build.cmd PackageTracerHome

# Build and run integration tests. Requires BuildTracerHome to have previously been run
.\build.cmd BuildAndRunWindowsIntegrationTests
```

### Linux

The recommended approach for Linux is to build using Docker. You can use this approach for both Windows and Linux hosts. The _build_in_docker.sh_ script automates building a Docker image with the required dependencies, and running the specified Nuke targets. For example:

```bash
# Clean and build the main tracer project
./build_in_docker.sh Clean BuildTracerHome

# Build and run managed unit tests. Requires BuildTracerHome to have previously been run
./build_in_docker.sh BuildAndRunManagedUnitTests

# Build and run integration tests. Requires BuildTracerHome to have previously been run
./build_in_docker.sh BuildAndRunLinuxIntegrationTests
```

### macOS

You can use Rider and CLion to develop on macOS. Building and testing can be done through the following Nuke targets:

```bash
# Clean and build the main tracer project
.\build.sh Clean BuildTracerHome

# Build and run managed and native unit tests. Requires BuildTracerHome to have previously been run
.\build.sh BuildAndRunManagedUnitTests BuildAndRunNativeUnitTests

# Build NuGet packages and MSIs. Requires BuildTracerHome to have previously been run
.\build.sh PackageTracerHome

# Build and run integration tests. Requires BuildTracerHome to have previously been run
.\build.sh BuildAndRunIntegrationTests
```

## Further Reading

Datadog APM
- [Datadog APM](https://docs.datadoghq.com/tracing/)
- [Datadog APM - Tracing .NET Core Applications](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core)
- [Datadog APM - Tracing .NET Framework Applications](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-framework)

Microsoft .NET Profiling APIs
- [Profiling API](https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/)
- [Metadata API](https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/metadata/)
- [The Book of the Runtime - Profiling](https://github.com/dotnet/coreclr/blob/master/Documentation/botr/profiling.md)

OpenTracing
- [OpenTracing documentation](https://github.com/opentracing/opentracing-csharp)
- [OpenTracing terminology](https://github.com/opentracing/specification/blob/master/specification.md)