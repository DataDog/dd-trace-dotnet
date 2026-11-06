# `Datadog.Trace.Bundle` NuGet package

This package contains the full Datadog .NET APM suite for Tracing (automatic, and custom), Continuous Profiler and Application Security Monitoring (ASM).

## Should I install this package?

Do **not** install this package if any of the following are true:

- You have already installed the [Datadog .NET Tracer MSI](https://docs.datadoghq.com/tracing/trace_collection/dd_libraries/dotnet-core/?tab=windows#install-the-tracer) on Windows. Install [Datadog.Trace](https://www.nuget.org/packages/Datadog.Trace) instead if you need custom instrumentation.
- You have already installed the [Datadog .NET package on Linux](https://docs.datadoghq.com/tracing/trace_collection/dd_libraries/dotnet-core/?tab=linux#install-the-tracer). Install [Datadog.Trace](https://www.nuget.org/packages/Datadog.Trace) instead if you need custom instrumentation.
- You instrument your application using the .NET dd-trace tool. Install [Datadog.Trace](https://www.nuget.org/packages/Datadog.Trace) instead if you need custom instrumentation.
- Your application is running in Azure App Services. Follow [our AAS guide](https://docs.datadoghq.com/serverless/azure_app_services/) instead.
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

## Reducing the published size

By default, `dotnet publish` copies the files for every supported platform and runtime into the `datadog` folder. To publish only the files your application needs, set the `DatadogPlatforms` MSBuild property to the platform(s) the application runs on:

```xml
<PropertyGroup>
  <DatadogPlatforms>linux-x64</DatadogPlatforms>
</PropertyGroup>
```

| Property | Description |
|----------|-------------|
| `DatadogPlatforms` | **Required.** One or more of `win-x64`, `win-x86`, `linux-x64`, `linux-arm64`, `linux-musl-x64`, `linux-musl-arm64`, `osx-x64`, `osx-arm64`, separated by `;`. Use the `linux-musl-*` values for Alpine Linux. |
| `DatadogRuntimes` | Optional. One or more target frameworks for the runtime(s) the application runs on, for example `net48` or `net6.0;net8.0`, separated by `;`. Defaults to the target framework being published. |

Nothing is inferred from the runtime identifier or target framework. If a platform or runtime is missing from these properties, automatic instrumentation will not work there. In particular, if the application can roll forward to a newer .NET version than it targets (for example, by setting `RollForward`), include that version in `DatadogRuntimes`.

You can also set the properties on the command line. MSBuild treats `;` as a separator on the command line, so either quote the value or escape the `;` as `%3B`:

```bash
dotnet publish -c Release '-p:DatadogPlatforms="linux-x64;linux-arm64"'
dotnet publish -c Release -p:DatadogPlatforms=linux-x64%3Blinux-arm64
```

This only applies to `dotnet publish` of SDK-style projects. If you don't set `DatadogPlatforms`, all files are published. The following errors and warnings may be reported:

| Code | Meaning |
|------|---------|
| `DDBUNDLE001` | Error: `DatadogPlatforms` requires MSBuild 16.7 or later (.NET SDK 3.1.400 or later). |
| `DDBUNDLE002` | Error: `DatadogPlatforms` contains an unsupported value. |
| `DDBUNDLE003` | Error: `DatadogRuntimes` contains an invalid value. Use a .NET Framework target framework, such as `net48`, or a .NET Core target framework for .NET Core 2.1 or later, such as `net8.0`. |
| `DDBUNDLE010` | Warning: the application is published for a `RuntimeIdentifier` that is not included in `DatadogPlatforms`. |
| `DDBUNDLE011` | Warning: `DatadogRuntimes` includes .NET Framework, but `DatadogPlatforms` does not include a Windows platform. |
| `DDBUNDLE012` | Warning: a .NET Framework application's `PlatformTarget` (`x86` or `x64`) is not included in `DatadogPlatforms`. |
| `DDBUNDLE013` | Warning: the application is published for a `RuntimeIdentifier` that isn't one of the `DatadogPlatforms` values, such as a version-specific RID like `win10-x64`, so it can't be validated. Make sure `DatadogPlatforms` includes the platform the application runs on. |

If you have checked a warning and it doesn't apply to your application, you can suppress it like any other build warning, by adding its code to the `NoWarn` property:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);DDBUNDLE013</NoWarn>
</PropertyGroup>
```

## Get in touch

If you have questions, feedback, or feature requests, reach our [support](https://docs.datadoghq.com/help).

