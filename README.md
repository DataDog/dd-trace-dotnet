# .NET Tracer for Datadog APM

**The .NET Tracer is currently in Alpha and is not recommended for use in production.**

Environment|Build Status
--|--
Windows|[![Build status](https://datadog-apm.visualstudio.com/dd-trace-csharp/_apis/build/status/Windows)](https://datadog-apm.visualstudio.com/dd-trace-csharp/_build/latest?definitionId=1)
Linux|[![Build status](https://datadog-apm.visualstudio.com/dd-trace-csharp/_apis/build/status/Linux)](https://datadog-apm.visualstudio.com/dd-trace-csharp/_build/latest?definitionId=2)

## Installation and Usage

Please [read our documentation](https://docs.datadoghq.com/tracing/setup/dotnet) for instructions on setting up .NET tracing and details about supported frameworks.

## Development

### The Components

**[Datadog Trace Agent](https://github.com/DataDog/datadog-trace-agent)**: A service that runs on your application servers, accepting trace data from the Datadog Tracer and sending it to Datadog. The Trace Agent is not part of this repo; it's the same Trace Agent to which all Datadog tracers (e.g. Go, Python, Java, Ruby) send data.

**[Datadog .NET Tracer](https://github.com/DataDog/dd-trace-csharp)**: This repository. A set of .NET libraries that let you trace any piece of your .NET code. Supports manual instrumentation and can automatically instrument supported libraries out-of-the-box.

### Windows

Minimum requirements to build the code in this repository:

- [Visual Studio 2017](https://visualstudio.microsoft.com/downloads/) v15.7 or newer
  - Workloads
    - Desktop development with C++
    - .NET desktop development
    - .NET Core cross-platform development
    - Optional: ASP.NET and web development (to build samples )
  - Individual components
    - .NET Framework 4.7 targeting pack
- [.NET Core 2.0 SDK](https://www.microsoft.com/net/download) or newer
- Optional: [WiX Toolset 3.11.1](http://wixtoolset.org/releases/) or newer to build Windows installer (msi)
  - Requires .NET Framework 3.5 SP2 (install from Windows Features control panel: `OptionalFeatures.exe`)
  - [WiX Toolset VS2017 Extension](https://marketplace.visualstudio.com/items?itemName=RobMensching.WixToolsetVisualStudio2017Extension) to build installer from VS2017
- Optional: [Docker for Windows](https://docs.docker.com/docker-for-windows/) to run some integration tests
  - Requires Windows 10 (1607 Anniversary Update, Build 14393 or newer)

Microsoft provides [evaluation developer VMs]((https://developer.microsoft.com/en-us/windows/downloads/virtual-machines)) with Windows 10 with Visual Studio 2017 pre-installed.


### Linux

Only manual instrumentation is supported on Linux at this time. Projects `Datadog.Trace`, `Datadog.Trace.OpenTracing`, and their respective test projects can be built on Linux when targeting .NET Core.

Requirements:
- [.NET Core SDK 2.0](https://www.microsoft.com/net/download) or newer
- [Mono](https://www.mono-project.com/download/stable/)
- [Docker](https://www.docker.com/)

Due to [this issue](https://github.com/dotnet/sdk/issues/335) in the .NET Core SDK, to build projects that target the .NET Framework and of , you'll need [this workaround](https://github.com/dotnet/netcorecli-fsc/wiki/.NET-Core-SDK-rc4#using-net-framework-as-targets-framework-the-osxunix-build-fails).

### CoreCLR submodule

This project makes use of git submodules to include required [CoreCLR](https://github.com/dotnet/coreclr) C++ headers. T build the C++ project, clone this repository with the `--recurse-submodules` option or run the following commands after cloning this repository:

```
git submodule init
git submodule update
```

### Running tests

The tests require the dependencies specified in `docker-compose.yaml` to be running on the same machine.
For this you need to have docker installed on your machine, and to start the dependencies with `./build.sh --target=dockerup`.

To build and run the tests on Windows:

```
./build.ps1
```

Or on Unix systems:

```
./build.sh
````

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
