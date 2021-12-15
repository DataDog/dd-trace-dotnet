# .NET Tracer for Datadog APM

## Installation and Usage

Please [read our documentation](https://docs.datadoghq.com/tracing/setup/dotnet) for instructions on setting up .NET tracing and details about supported frameworks.

## Downloads
Package|Download
-|-
Windows and Linux Installers|[See releases](https://github.com/DataDog/dd-trace-dotnet/releases)
`Datadog.Trace`|[![Datadog.Trace](https://img.shields.io/nuget/vpre/Datadog.Trace.svg)](https://www.nuget.org/packages/Datadog.Trace)
`Datadog.Trace.OpenTracing`|[![Datadog.Trace.OpenTracing](https://img.shields.io/nuget/vpre/Datadog.Trace.OpenTracing.svg)](https://www.nuget.org/packages/Datadog.Trace.OpenTracing)

## Build Status on `master`

Pipeline Stage         | Windows     |Linux   |  
-----------------------|-------------|--------|
Build                  | [![Build Windows](https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/status/consolidated-pipeline?branchName=master&stageName=build_windows)](https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/latest?definitionId=54&branchName=master) | [![Build Linux](https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/status/consolidated-pipeline?branchName=master&stageName=build_linux)](https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/latest?definitionId=54&branchName=master) 
Unit Tests             | [![Build Status](https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/status/consolidated-pipeline?branchName=master&stageName=build_windows)](https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/latest?definitionId=54&branchName=master) | [![Build Status](https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/status/consolidated-pipeline?branchName=master&stageName=unit_tests_linux)](https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/latest?definitionId=54&branchName=master) 
Integration Tests       | [![Build Status](https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/status/consolidated-pipeline?branchName=master&stageName=integration_tests_windows_iis)](https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/latest?definitionId=54&branchName=master) [![Build Status](https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/status/consolidated-pipeline?branchName=master&stageName=integration_tests_windows)](https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/latest?definitionId=54&branchName=master) | [![Build Status](https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/status/consolidated-pipeline?branchName=master&stageName=integration_tests_linux)](https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/latest?definitionId=54&branchName=master)

# Development

## Components

**[Datadog Agent](https://github.com/DataDog/datadog-agent)**: A service that runs on your application servers, accepting trace data from the Datadog Tracer and sending it to Datadog. The Agent is not part of this repo; it's the same Agent to which all Datadog tracers (e.g. Go, Python, Java, Ruby) send data.

**[Datadog .NET Tracer](https://github.com/DataDog/dd-trace-dotnet)**: This repository. A set of .NET libraries that let you trace any piece of your .NET code. Supports manual instrumentation and can automatically instrument supported libraries out-of-the-box.

## Windows

### Minimum requirements

- [Visual Studio 2019 (16.8)](https://visualstudio.microsoft.com/downloads/) or newer
  - Workloads
    - Desktop development with C++
    - .NET desktop development
    - .NET Core cross-platform development
    - Optional: ASP.NET and web development (to build samples)
  - Individual components
    - .NET Framework 4.7 targeting pack
- [.NET 5.0 SDK](https://dotnet.microsoft.com/download/dotnet/5.0)
- [.NET 5.0 x86 SDK](https://dotnet.microsoft.com/download/dotnet/5.0) to run 32-bit tests locally
- Optional: [ASP.NET Core 2.1 Runtime](https://dotnet.microsoft.com/download/dotnet-core/2.1) to test in .NET Core 2.1 locally.
- Optional: [ASP.NET Core 3.0 Runtime](https://dotnet.microsoft.com/download/dotnet-core/3.0) to test in .NET Core 3.0 locally.
- Optional: [ASP.NET Core 3.1 Runtime](https://dotnet.microsoft.com/download/dotnet-core/3.1) to test in .NET Core 3.1 locally.
- Optional: [nuget.exe CLI](https://www.nuget.org/downloads) v5.3 or newer
- Optional: [WiX Toolset 3.11.1](http://wixtoolset.org/releases/) or newer to build Windows installer (msi)
  - [WiX Toolset Visual Studio Extension](https://wixtoolset.org/releases/) to build installer from Visual Studio
- Optional: [Docker for Windows](https://docs.docker.com/docker-for-windows/) to build Linux binaries and run integration tests on Linux containers. See [section on Docker Compose](#building-and-running-tests-with-docker-compose).
  - Requires Windows 10 (1607 Anniversary Update, Build 14393 or newer)

Microsoft provides [evaluation developer VMs](https://developer.microsoft.com/en-us/windows/downloads/virtual-machines) with Windows 10 and Visual Studio pre-installed.

### Building from a command line

This repository uses [Nuke](https://nuke.build/) for build automation. To see a list of possible targets run:

```cmd
.\tracer\build.cmd --help
```

For example:

```powershell
# Clean and build the main tracer project
.\tracer\build.cmd Clean BuildTracerHome

# Build and run managed and native unit tests. Requires BuildTracerHome to have previously been run
.\tracer\build.cmd BuildAndRunManagedUnitTests BuildAndRunNativeUnitTests 

# Build NuGet packages and MSIs. Requires BuildTracerHome to have previously been run
.\tracer\build.cmd PackageTracerHome 

# Build and run integration tests. Requires BuildTracerHome to have previously been run
.\tracer\build.cmd BuildAndRunWindowsIntegrationTests
```

## Linux

The recommended approach for Linux is to build using Docker. You can use this approach for both Windows and Linux hosts. The _build_in_docker.sh_ script automates building a Docker image with the required dependencies, and running the specified Nuke targets. For example:

```bash
# Clean and build the main tracer project
./tracer/build_in_docker.sh Clean BuildTracerHome

# Build and run managed unit tests. Requires BuildTracerHome to have previously been run
./tracer/build_in_docker.sh BuildAndRunManagedUnitTests 

# Build and run integration tests. Requires BuildTracerHome to have previously been run
./tracer/build_in_docker.sh BuildAndRunLinuxIntegrationTests
```

## Further Reading

Datadog APM
- [Datadog APM](https://docs.datadoghq.com/tracing/)
- [Datadog APM - Tracing .NET Core and .NET 5 Applications](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core)
- [Datadog APM - Tracing .NET Framework Applications](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-framework)

Microsoft .NET Profiling APIs
- [Profiling API](https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/)
- [Metadata API](https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/metadata/)
- [The Book of the Runtime - Profiling](https://github.com/dotnet/coreclr/blob/master/Documentation/botr/profiling.md)

OpenTracing
- [OpenTracing documentation](https://github.com/opentracing/opentracing-csharp)
- [OpenTracing terminology](https://github.com/opentracing/specification/blob/master/specification.md)

## Get in touch

If you have questions, feedback, or feature requests, reach our [support](https://docs.datadoghq.com/help).
