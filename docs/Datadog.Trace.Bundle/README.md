# `Datadog.Trace.Bundle` NuGet package

This package contains the Datadog .NET APM suite. As other setups, it enables Tracing, Continuous Profiler and ASM. For tracing though, it allows both automatic and custom instrumentation. It is meant to be used mainly when access to the application server is limited (eg on Azure Service Fabric). Refer to the next section for more details on when to use this package.

> If you are using automatic instrumentation and would like to interact with APM only through C# attributes, see the [Datadog.Trace.Annotations](https://www.nuget.org/packages/Datadog.Trace.Annotations/) NuGet package.

## When should you use Datadog.Trace.Bundle

Consider using this package mainly in scenarios where other setups aren't possible. 
For instance, [on Windows, please install the tracer using our MSI](https://docs.datadoghq.com/tracing/trace_collection/dd_libraries/dotnet-core/?tab=windows#install-the-tracer), on [linux, using the appropriate package](https://docs.datadoghq.com/tracing/trace_collection/dd_libraries/dotnet-core/?tab=linux#install-the-tracer), on AAS Windows, rely on the [AAS extension](https://docs.datadoghq.com/serverless/azure_app_services/?tab=net). 

Also note, that `Datadog.Trace.Bundle` will not allow you to trace apps running in IIS.

## Getting Started

1. Configure the Datadog agent for APM [as described in our documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core#configure-the-datadog-agent-for-apm).
2. Add the `Datadog.Trace.Bundle` to your project.
3. Configure the tracer on the server, as shown below
4. [View your live data on Datadog](https://app.datadoghq.com/apm/traces).

### Configure the tracer

In addition to adding the nuget to your project, you need to set the following required environment variables for automatic instrumentation to attach to your application.

> **_NOTE:_** 
The following are the mandatory variables. More options are available in our public documentation for the [Tracer](https://docs.datadoghq.com/tracing/trace_collection/library_config/dotnet-core/?tab=environmentvariables) and the [Continuous Profiler](https://docs.datadoghq.com/profiler/enabling/dotnet/?tab=linux#configuration) to tune your usage. 


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

The value for the `<APP_DIRECTORY>`  placeholder is the path to the directory containing the application’s .dll files. The value for the `CORECLR_PROFILER_PATH` environment variable varies based on the system where the application is running:

| OPERATING SYSTEM AND PROCESS ARCHITECTURE      | CORECLR_PROFILER_PATH VALUE |
| ----------- | ----------- |
| Alpine Linux x64      | <APP_DIRECTORY>/datadog/linux-musl-x64/Datadog.Trace.ClrProfiler.Native.so       |
| Linux x64   | <APP_DIRECTORY>/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so        |
| Linux ARM64      | <APP_DIRECTORY>/datadog/linux-arm64/Datadog.Trace.ClrProfiler.Native.so       |
| Windows x64   | <APP_DIRECTORY>\datadog\win-x64\Datadog.Trace.ClrProfiler.Native.dll        |
| Windows x86      | <APP_DIRECTORY>\datadog\win-x86\Datadog.Trace.ClrProfiler.Native.dll       |

For Docker images running on Linux, configure the image to run the createLogPath.sh script:

```
RUN /<APP_DIRECTORY>/datadog/createLogPath.sh
```

Docker examples are available [here](https://github.com/DataDog/dd-trace-dotnet/tree/master/tracer/samples/NugetDeployment)

For standalone applications, manually restart the application.

## Get in touch

If you have questions, feedback, or feature requests, reach our [support](https://docs.datadoghq.com/help).

