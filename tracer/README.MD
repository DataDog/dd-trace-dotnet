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
    - When opening a solution, Visual Studio will prompt you to install any missing components.
      The prompt will appear in the "Solution Explorer". A list of all recommended components can be found in our [.vsconfig](../.vsconfig)-file.
- [.NET 7.0 SDK](https://dotnet.microsoft.com/download/dotnet/7.0)
  - Optional: [.NET 7.0 x86 SDK](https://dotnet.microsoft.com/download/dotnet/7.0) to run 32-bit tests locally
- Optional: ASP.NET Core Runtimes to run tests locally
  - [ASP.NET Core 2.1](https://dotnet.microsoft.com/download/dotnet/2.1)
  - [ASP.NET Core 3.0](https://dotnet.microsoft.com/download/dotnet/3.0)
  - [ASP.NET Core 3.1](https://dotnet.microsoft.com/download/dotnet/3.1)
  - [ASP.NET Core 5.0](https://dotnet.microsoft.com/download/dotnet/5.0)
  - [ASP.NET Core 6.0](https://dotnet.microsoft.com/download/dotnet/6.0)
- Optional: [nuget.exe CLI](https://www.nuget.org/downloads) v5.3 or newer
- Optional: [WiX Toolset 3.11.1](http://wixtoolset.org/releases/) or newer to build Windows installer (msi)
  - [WiX Toolset Visual Studio Extension](https://wixtoolset.org/releases/) to build installer from Visual Studio
- Optional: [Docker for Windows](https://docs.docker.com/docker-for-windows/) to build Linux binaries and run integration tests on Linux containers.
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

The recommended approach for Linux is to build using Docker. You can use this approach for both Windows and Linux hosts. The _build_in_docker.sh_ script automates building a Docker image with the required dependencies, and running the specified Nuke targets. For example, on Linux:

```bash
# Clean and build the main tracer project
./build_in_docker.sh Clean BuildTracerHome

# Build and run managed unit tests. Requires BuildTracerHome to have previously been run
./build_in_docker.sh BuildAndRunManagedUnitTests

# Build and run integration tests. Requires BuildTracerHome to have previously been run
./build_in_docker.sh BuildAndRunLinuxIntegrationTests
```

Alternatively, on Windows:
```powershell
./build_in_docker.ps1 BuildTracerHome BuildAndRunLinuxIntegrationTests
```

### macOS

You can use Rider and CLion to develop on macOS. Building and testing can be done through the following Nuke targets:

```bash
# Clean and build the main tracer project
./build.sh Clean BuildTracerHome

# Build and run managed and native unit tests. Requires BuildTracerHome to have previously been run
./build.sh BuildAndRunManagedUnitTests BuildAndRunNativeUnitTests

# Build NuGet packages and MSIs. Requires BuildTracerHome to have previously been run
./build.sh PackageTracerHome

# Start IntegrationTests dependencies.
docker-compose up StartDependencies.OSXARM64

# Build and run integration tests. Requires BuildTracerHome to have previously been run
./build.sh BuildAndRunOsxIntegrationTests

# Build and run integration tests filtering on one framework, one set of tests and a sample app.
./build.sh BuildAndRunOsxIntegrationTests --framework "net6.0" --filter "rabbit" --SampleName "Samples.Rabbit"

# Stop IntegrationTests dependencies.
docker-compose down
```

## Additional Technical Documentation

* [Implementing an automatic instrumentation](../docs/development/AutomaticInstrumentation.md)
* [Duck typing: usages, best practices, and benchmarks](../docs/development/DuckTyping.md)
* [Datadog.Trace NuGet package README](../docs/Datadog.Trace/README.md)

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
