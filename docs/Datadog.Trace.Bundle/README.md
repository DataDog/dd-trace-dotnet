# `Datadog.Trace.Bundle` NuGet package

This package contains the full Datadog .NET APM suite for Tracing (automatic, and custom), Continuous Profiler and Application Security Monitoring (ASM).

## Should I install this package?

Do **not** install this package if any of the following are true:

- You have already installed the [Datadog .NET Tracer MSI](https://docs.datadoghq.com/tracing/trace_collection/dd_libraries/dotnet-core/?tab=windows#install-the-tracer) on Windows. Install [Datadog.Trace](https://www.nuget.org/packages/Datadog.Trace) instead if you need custom instrumentation.
- You have already installed the [Datadog .NET package on Linux](https://docs.datadoghq.com/tracing/trace_collection/dd_libraries/dotnet-core/?tab=linux#install-the-tracer). Install [Datadog.Trace](https://www.nuget.org/packages/Datadog.Trace) instead if you need custom instrumentation.
- You instrument your application using the .NET dd-trace tool. Install [Datadog.Trace](https://www.nuget.org/packages/Datadog.Trace) instead if you need custom instrumentation.
- Your application is running in Azure App Services. Install [the AAS extension](https://docs.datadoghq.com/serverless/azure_app_services/?tab=net) instead.
- Your application is running in IIS on Windows. This package is not supported; [install the MSI](https://docs.datadoghq.com/tracing/trace_collection/dd_libraries/dotnet-core/?tab=windows#install-the-tracer) instead, and install [Datadog.Trace](https://www.nuget.org/packages/Datadog.Trace) if you also need custom instrumentation.

Install this package if:

- You are running in a scenario where you can't install an MSI or Linux package. Services such as Azure Service Fabric provide limited access to the underlying server.
- You want to use both custom and automatic instrumentation, and keeping tracer versions in-sync is a chore.
- You are running multiple applications on a server and some of them use custom instrumentation, as well as automatic instrumentation.
- You prefer to deploy a static set of binaries rather than relying on existing infrastructure on the host.

> If you are using automatic instrumentation and would like to interact with APM only through C# attributes, see the [Datadog.Trace.Annotations](https://www.nuget.org/packages/Datadog.Trace.Annotations/) NuGet package.

## What does Datadog.Trace.Bundle contain?

Datadog.Trace.Bundle contains two things:

- A reference to the [Datadog.Trace NuGet package](https://www.nuget.org/packages/Datadog.Trace) for custom instrumentation.
- The native binaries required for automatic instrumentation, for the Continuous Profiler, and for ASM.

These native binaries are identical to those installed by the MSI and Linux installer packages, so Datadog.Trace.Bundle should be considered an alternative deployment mechanism for automatic instrumentation. 

The main advantages of Datadog.Trace.Bundle over the MSI or Linux packages are:
- You can use it in locations where you cannot access the underlying host to install the MSI or Linux package.
- You can have multiple applications on the same host using different versions of Datadog.Trace.Bundle without issue.

## Getting Started

1. Configure the Datadog agent for APM [as described in our documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core#configure-the-datadog-agent-for-apm).
2. Add the [Datadog.Trace.Bundle](https://www.nuget.org/packages/Datadog.Trace.Bundle) NuGet package to your project, using `dotnet add package Datadog.Trace.Bundle`, for example.
3. Configure the tracer on the server, as [described below](#configure-the-tracer)
4. [View your live data on Datadog](https://app.datadoghq.com/apm/traces).

### Configure the tracer

After adding the NuGet package to your project, set the following **required** environment variables to enable automatic instrumentation of your application and restart the application.

> **_NOTE:_** 
The following are the mandatory variables. For further configuration options, see our public documentation for the [Tracer](https://docs.datadoghq.com/tracing/trace_collection/library_config/dotnet-core/?tab=environmentvariables) and the [Continuous Profiler](https://docs.datadoghq.com/profiler/enabling/dotnet/?tab=linux#configuration).


.NET Core:

```
CORECLR_ENABLE_PROFILING=1
CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
CORECLR_PROFILER_PATH=<System-dependent path>
DD_DOTNET_TRACER_HOME=<APP_DIRECTORY>/datadog
```

.NET Framework:

```
COR_ENABLE_PROFILING=1
COR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
COR_PROFILER_PATH=<System-dependent path>
DD_DOTNET_TRACER_HOME=<APP_DIRECTORY>/datadog
```

The value for the `<APP_DIRECTORY>` placeholder is the path to the directory containing the application’s .dll files. The value for the `CORECLR_PROFILER_PATH`/`COR_PROFILER_PATH` environment variable varies based on the system where the application is running:

| OPERATING SYSTEM AND PROCESS ARCHITECTURE | CORECLR_PROFILER_PATH VALUE                                                  |
|-------------------------------------------|------------------------------------------------------------------------------|
| Alpine Linux x64                          | <APP_DIRECTORY>/datadog/linux-musl-x64/Datadog.Trace.ClrProfiler.Native.so   |
| Linux x64                                 | <APP_DIRECTORY>/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so        |
| Alpine Linux ARM64                        | <APP_DIRECTORY>/datadog/linux-musl-arm64/Datadog.Trace.ClrProfiler.Native.so |
| Linux ARM64                               | <APP_DIRECTORY>/datadog/linux-arm64/Datadog.Trace.ClrProfiler.Native.so      |
| Windows x64                               | <APP_DIRECTORY>\datadog\win-x64\Datadog.Trace.ClrProfiler.Native.dll         |
| Windows x86                               | <APP_DIRECTORY>\datadog\win-x86\Datadog.Trace.ClrProfiler.Native.dll         |

For Docker images running on Linux, configure the image to run the createLogPath.sh script:

```
RUN /<APP_DIRECTORY>/datadog/createLogPath.sh
```

### Examples

Docker examples are available [here](https://github.com/DataDog/dd-trace-dotnet/tree/master/tracer/samples/NugetDeployment)

## Get in touch

If you have questions, feedback, or feature requests, reach our [support](https://docs.datadoghq.com/help).

