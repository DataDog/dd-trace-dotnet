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

Pipeline          | Build Status
------------------|-------------
Unit tests        | [![Build Status](https://dev.azure.com/datadog-apm/dd-trace-dotnet/_apis/build/status/unit-tests?branchName=master)](https://dev.azure.com/datadog-apm/dd-trace-dotnet/_build/latest?definitionId=28&branchName=master)
Integration tests | [![Build Status](https://dev.azure.com/datadog-apm/dd-trace-dotnet/_apis/build/status/integration-tests?branchName=master)](https://dev.azure.com/datadog-apm/dd-trace-dotnet/_build/latest?definitionId=27&branchName=master)

# Development

## Components

**[Datadog Agent](https://github.com/DataDog/datadog-agent)**: A service that runs on your application servers, accepting trace data from the Datadog Tracer and sending it to Datadog. The Agent is not part of this repo; it's the same Agent to which all Datadog tracers (e.g. Go, Python, Java, Ruby) send data.

**[Datadog .NET Tracer](https://github.com/DataDog/dd-trace-dotnet)**: This repository. A set of .NET libraries that let you trace any piece of your .NET code. Supports manual instrumentation and can automatically instrument supported libraries out-of-the-box.

## Windows

### Minimum requirements

- [Visual Studio 2019](https://visualstudio.microsoft.com/downloads/) or newer
  - Workloads
    - Desktop development with C++
    - .NET desktop development
    - .NET Core cross-platform development
    - Optional: ASP.NET and web development (to build samples)
  - Individual components
    - .NET Framework 4.7 targeting pack
- [.NET Core 3.1 SDK](https://dotnet.microsoft.com/download/dotnet-core/3.1)
- Optional: [.NET Core 2.1 Runtime](https://dotnet.microsoft.com/download/dotnet-core/2.1) to test in .NET Core 2.1 locally.
- Optional: [.NET Core 3.0 Runtime](https://dotnet.microsoft.com/download/dotnet-core/3.0) to test in .NET Core 3.0 locally.
- Optional: [nuget.exe CLI](https://www.nuget.org/downloads) v5.3 or newer
- Optional: [WiX Toolset 3.11.1](http://wixtoolset.org/releases/) or newer to build Windows installer (msi)
  - Requires .NET Framework 3.5 SP2 (install from Windows Features control panel: `OptionalFeatures.exe`)
  - [WiX Toolset Visual Studio Extension](https://wixtoolset.org/releases/) to build installer from Visual Studio
- Optional: [Docker for Windows](https://docs.docker.com/docker-for-windows/) to build Linux binaries and run integration tests on Linux containers. See [section on Docker Compose](#building-and-running-tests-with-docker-compose).
  - Requires Windows 10 (1607 Anniversary Update, Build 14393 or newer)

Microsoft provides [evaluation developer VMs](https://developer.microsoft.com/en-us/windows/downloads/virtual-machines) with Windows 10 and Visual Studio pre-installed.

### Building from a command line

From a _Developer Command Prompt for VS 2019_:

```cmd
rem Restore NuGet packages
rem nuget.exe is required for command line restore because msbuild doesn't support packages.config
rem (see https://github.com/NuGet/Home/issues/7386)
nuget restore Datadog.Trace.sln

rem Build C# projects (Platform: always AnyCPU)
msbuild Datadog.Trace.proj /t:BuildCsharp /p:Configuration=Release

rem Build NuGet packages
dotnet pack src\Datadog.Trace\Datadog.Trace.csproj
dotnet pack src\Datadog.Trace.OpenTracing\Datadog.Trace.OpenTracing.csproj

rem Build C++ projects
rem The native profiler depends on the Datadog.Trace.ClrProfiler.Managed.Loader C# project so be sure that is built first
msbuild Datadog.Trace.proj /t:BuildCpp /p:Configuration=Release;Platform=x64
msbuild Datadog.Trace.proj /t:BuildCpp /p:Configuration=Release;Platform=x86

rem Build MSI installer for Windows x64 (supports both x64 and x86 apps)
msbuild Datadog.Trace.proj /t:msi /p:Configuration=Release;Platform=x64

rem Build MSI installer for Windows x86 (supports x86 apps only)
msbuild Datadog.Trace.proj /t:msi /p:Configuration=Release;Platform=x86

rem Build tracer home directory for Windows (x64 and x86)
msbuild Datadog.Trace.proj /t:CreateHomeDirectory /p:Configuration=Release;Platform=All
```

## Linux

### Minimum requirements

To build C# projects and NuGet packages only
- [.NET Core SDK 3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1)

To build everything and run integration tests
- [Docker Compose](https://docs.docker.com/compose/install/)

### Building and running tests with Docker Compose

You can use [Docker Compose](https://docs.docker.com/compose/) with Linux containers to build Linux binaries and run the test suites. This works on both Linux and Windows hosts.

```bash
# build C# projects
docker-compose run build

# build C++ project
docker-compose run Profiler

# run integration tests
docker-compose run IntegrationTests
```

## Further Reading

Datadog APM
- [Datadog APM](https://docs.datadoghq.com/tracing/)
- [Datadog APM - Tracing .NET Applications](https://docs.datadoghq.com/tracing/setup/dotnet/)
- [Datadog APM - Advanced Usage](https://docs.datadoghq.com/tracing/advanced_usage/?tab=dotnet)

Microsoft .NET Profiling APIs
- [Profiling API](https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/)
- [Metadata API](https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/metadata/)
- [The Book of the Runtime - Profiling](https://github.com/dotnet/coreclr/blob/master/Documentation/botr/profiling.md)

OpenTracing
- [OpenTracing documentation](https://github.com/opentracing/opentracing-csharp)
- [OpenTracing terminology](https://github.com/opentracing/specification/blob/master/specification.md)

## Get in touch

If you have questions, feedback, or feature requests, reach our [support](https://docs.datadoghq.com/help).
